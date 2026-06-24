using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Winsvc.Contracts.Manifest;
using Winsvc.Core;

namespace Winsvc.Infrastructure;

public sealed class FileSystemWatcherManifestProvider : IManifestProvider, IHostedService, IDisposable
{
    static readonly TimeSpan ReloadDelay = TimeSpan.FromMilliseconds(250);

    readonly IManifestReader manifestReader;
    readonly IManifestValidator manifestValidator;
    readonly ILogger<FileSystemWatcherManifestProvider> logger;
    readonly string manifestDirectory;
    readonly bool hotReloadEnabled;
    readonly SemaphoreSlim reloadLock = new(1, 1);
    readonly object reloadGate = new();

    FileSystemWatcher? watcher;
    CancellationTokenSource? pendingReload;
    Task? pendingReloadTask;
    IReadOnlyList<ServiceManifest> manifests = Array.Empty<ServiceManifest>();
    bool disposed;

    public FileSystemWatcherManifestProvider(
        IManifestReader manifestReader,
        IManifestValidator manifestValidator,
        string manifestDirectory,
        bool hotReloadEnabled,
        ILogger<FileSystemWatcherManifestProvider> logger)
    {
        this.manifestReader = manifestReader;
        this.manifestValidator = manifestValidator;
        this.manifestDirectory = Path.GetFullPath(manifestDirectory);
        this.hotReloadEnabled = hotReloadEnabled;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);

        if (!hotReloadEnabled)
        {
            logger.LogInformation("Manifest hot reload is disabled.");
            return;
        }

        if (!Directory.Exists(manifestDirectory))
        {
            logger.LogWarning("Manifest directory does not exist: {ManifestDirectory}", manifestDirectory);
            return;
        }

        watcher = new FileSystemWatcher(manifestDirectory)
        {
            Filter = "*.y*ml",
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.LastWrite
                | NotifyFilters.CreationTime
                | NotifyFilters.Size
        };

        watcher.Created += OnManifestDirectoryChanged;
        watcher.Changed += OnManifestDirectoryChanged;
        watcher.Deleted += OnManifestDirectoryChanged;
        watcher.Renamed += OnManifestRenamed;
        watcher.EnableRaisingEvents = true;

        logger.LogInformation("Manifest hot reload is watching {ManifestDirectory}", manifestDirectory);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        watcher?.Dispose();
        watcher = null;

        CancellationTokenSource? tokenSource;
        Task? reloadTask;
        lock (reloadGate)
        {
            tokenSource = pendingReload;
            reloadTask = pendingReloadTask;
            pendingReload = null;
            pendingReloadTask = null;
        }

        tokenSource?.Cancel();
        if (reloadTask is not null)
        {
            try
            {
                await reloadTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    public Task<IReadOnlyList<ServiceManifest>> GetManifestsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(manifests);
    }

    public Task<ServiceManifest?> GetManifestAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manifest = manifests.FirstOrDefault(manifest => string.Equals(manifest.Id, id, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(manifest);
    }

    void OnManifestDirectoryChanged(object sender, FileSystemEventArgs args)
    {
        if (IsManifestPath(args.FullPath))
        {
            ScheduleReload();
        }
    }

    void OnManifestRenamed(object sender, RenamedEventArgs args)
    {
        if (IsManifestPath(args.FullPath) || IsManifestPath(args.OldFullPath))
        {
            ScheduleReload();
        }
    }

    void ScheduleReload()
    {
        CancellationTokenSource tokenSource;

        lock (reloadGate)
        {
            pendingReload?.Cancel();
            pendingReload = new CancellationTokenSource();
            tokenSource = pendingReload;
            pendingReloadTask = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(ReloadDelay, tokenSource.Token);
                    await RefreshAsync(tokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to hot reload manifests from {ManifestDirectory}", manifestDirectory);
                }
                finally
                {
                    tokenSource.Dispose();
                }
            });
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await reloadLock.WaitAsync(cancellationToken);
        try
        {
            var loaded = new List<ServiceManifest>();
            foreach (var path in ManifestPathResolver.EnumerateManifestPaths(manifestDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var manifest = await manifestReader.ReadAsync(path);
                    var errors = manifestValidator.Validate(manifest).ToArray();
                    if (errors.Length > 0)
                    {
                        logger.LogWarning(
                            "Skipping invalid manifest {ManifestPath}: {Errors}",
                            path,
                            string.Join("; ", errors));
                        continue;
                    }

                    loaded.Add(manifest);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Skipping unreadable manifest {ManifestPath}", path);
                }
            }

            manifests = loaded
                .OrderBy(manifest => manifest.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            logger.LogInformation(
                "Loaded {ManifestCount} manifest(s) from {ManifestDirectory}",
                manifests.Count,
                manifestDirectory);
        }
        finally
        {
            reloadLock.Release();
        }
    }

    static bool IsManifestPath(string path)
    {
        return !ManifestPathResolver.IsTemplatePath(path)
            && (path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        reloadLock.Dispose();
        disposed = true;
    }
}