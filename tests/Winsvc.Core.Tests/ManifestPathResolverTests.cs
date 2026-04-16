using System;
using System.IO;
using System.Threading;
using Winsvc.Core;
using Xunit;

namespace Winsvc.Core.Tests;

public sealed class ManifestPathResolverTests
{
    [Fact]
    public void ResolveDirectory_FindsManifestsInAncestorDirectory()
    {
        using var workspace = TemporaryDirectory.Create();
        var projectDirectory = Path.Combine(workspace.Path, "src", "Winsvc.Cli");
        var manifestDirectory = Path.Combine(workspace.Path, "manifests");
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(manifestDirectory);

        using var _ = new EnvironmentVariableScope(ManifestPathResolver.ManifestDirectoryEnvironmentVariable, null);
        var resolved = ManifestPathResolver.ResolveDirectory(configuredPath: null, projectDirectory);

        Assert.Equal(Path.GetFullPath(manifestDirectory), resolved);
    }

    [Fact]
    public void ResolveDirectory_PrefersConfiguredPath()
    {
        using var workspace = TemporaryDirectory.Create();
        var projectDirectory = Path.Combine(workspace.Path, "src", "Winsvc.Cli");
        var configuredManifestDirectory = Path.Combine(workspace.Path, "custom-manifests");
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(configuredManifestDirectory);
        Directory.CreateDirectory(Path.Combine(workspace.Path, "manifests"));

        using var _ = new EnvironmentVariableScope(ManifestPathResolver.ManifestDirectoryEnvironmentVariable, null);
        var resolved = ManifestPathResolver.ResolveDirectory(configuredManifestDirectory, projectDirectory);

        Assert.Equal(Path.GetFullPath(configuredManifestDirectory), resolved);
    }

    [Fact]
    public void ResolveDirectory_PrefersEnvironmentVariableWhenConfiguredPathIsNotSet()
    {
        using var workspace = TemporaryDirectory.Create();
        var projectDirectory = Path.Combine(workspace.Path, "src", "Winsvc.Cli");
        var environmentManifestDirectory = Path.Combine(workspace.Path, "env-manifests");
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(environmentManifestDirectory);
        Directory.CreateDirectory(Path.Combine(workspace.Path, "manifests"));

        using var _ = new EnvironmentVariableScope(
            ManifestPathResolver.ManifestDirectoryEnvironmentVariable,
            environmentManifestDirectory);
        var resolved = ManifestPathResolver.ResolveDirectory(configuredPath: null, projectDirectory);

        Assert.Equal(Path.GetFullPath(environmentManifestDirectory), resolved);
    }

    sealed class EnvironmentVariableScope : IDisposable
    {
        static readonly object Sync = new();

        readonly string key;
        readonly string? originalValue;
        bool disposed;

        public EnvironmentVariableScope(string key, string? value)
        {
            this.key = key;
            Monitor.Enter(Sync);
            originalValue = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            Environment.SetEnvironmentVariable(key, originalValue);
            Monitor.Exit(Sync);
            disposed = true;
        }
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
