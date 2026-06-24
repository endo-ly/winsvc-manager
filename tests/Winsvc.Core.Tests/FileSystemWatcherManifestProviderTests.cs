using Microsoft.Extensions.Logging.Abstractions;
using Winsvc.Infrastructure;

namespace Winsvc.Core.Tests;

public sealed class FileSystemWatcherManifestProviderTests
{
    [Fact]
    public async Task StartAsync_LoadsValidManifestsAndSkipsTemplates()
    {
        using var workspace = TemporaryDirectory.Create();
        await WriteManifestAsync(workspace.Path, "alpha", "Alpha");
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "service.template.yaml"), "id: template");
        using var provider = CreateProvider(workspace.Path, hotReloadEnabled: false);

        await provider.StartAsync(CancellationToken.None);

        var manifests = await provider.GetManifestsAsync();
        Assert.Single(manifests);
        Assert.Equal("alpha", manifests[0].Id);
    }

    [Fact]
    public async Task RefreshAsync_UpdatesCacheWhenManifestChanges()
    {
        using var workspace = TemporaryDirectory.Create();
        await WriteManifestAsync(workspace.Path, "alpha", "Alpha");
        using var provider = CreateProvider(workspace.Path, hotReloadEnabled: false);
        await provider.StartAsync(CancellationToken.None);

        await WriteManifestAsync(workspace.Path, "alpha", "Alpha Reloaded");
        await provider.RefreshAsync();

        var manifest = await provider.GetManifestAsync("alpha");
        Assert.Equal("Alpha Reloaded", manifest?.DisplayName);
    }

    [Fact]
    public async Task RefreshAsync_RemovesDeletedManifestFromCache()
    {
        using var workspace = TemporaryDirectory.Create();
        var manifestPath = await WriteManifestAsync(workspace.Path, "alpha", "Alpha");
        using var provider = CreateProvider(workspace.Path, hotReloadEnabled: false);
        await provider.StartAsync(CancellationToken.None);

        File.Delete(manifestPath);
        await provider.RefreshAsync();

        Assert.Null(await provider.GetManifestAsync("alpha"));
    }

    [Fact]
    public async Task HotReloadDisabled_KeepsInitialCacheUntilRefresh()
    {
        using var workspace = TemporaryDirectory.Create();
        await WriteManifestAsync(workspace.Path, "alpha", "Alpha");
        using var provider = CreateProvider(workspace.Path, hotReloadEnabled: false);
        await provider.StartAsync(CancellationToken.None);

        await WriteManifestAsync(workspace.Path, "alpha", "Alpha Reloaded");

        var manifest = await provider.GetManifestAsync("alpha");
        Assert.Equal("Alpha", manifest?.DisplayName);
    }

    static FileSystemWatcherManifestProvider CreateProvider(string manifestDirectory, bool hotReloadEnabled)
    {
        return new FileSystemWatcherManifestProvider(
            new YamlManifestReader(),
            new ManifestValidator(),
            manifestDirectory,
            hotReloadEnabled,
            NullLogger<FileSystemWatcherManifestProvider>.Instance);
    }

    static async Task<string> WriteManifestAsync(string manifestDirectory, string id, string displayName)
    {
        var path = Path.Combine(manifestDirectory, $"{id}.yaml");
        var yaml = $@"
id: {id}
displayName: {displayName}
description: Test service
runtime:
  workDir: C:\svc\runtimes\{id}
  executable: C:\svc\runtimes\{id}\app.exe
service:
  wrapperDir: C:\svc\services\{id}
health:
  url: http://127.0.0.1:8010/health
  timeoutSec: 5
";
        await File.WriteAllTextAsync(path, yaml);
        return path;
    }

    sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; }

        TemporaryDirectory(string path)
        {
            Path = path;
        }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "winsvc-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
