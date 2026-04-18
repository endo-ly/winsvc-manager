using System;
using System.IO;
using System.Threading.Tasks;
using Winsvc.Contracts.Manifest;
using Winsvc.Infrastructure;
using Xunit;

namespace Winsvc.Core.Tests;

public sealed class WinSwServiceManagerTests
{
    [Fact]
    public async Task InstallAsync_CopiesBundledWinSwToWrapperExecutable_WhenWrapperExeIsMissing()
    {
        using var temp = TemporaryDirectory.Create();
        var wrapperDir = Path.Combine(temp.Path, "wrapper");
        Directory.CreateDirectory(wrapperDir);

        // Put a placeholder source as bundled winsw.exe candidate.
        var bundledWinSw = Path.Combine(wrapperDir, "winsw.exe");
        await File.WriteAllTextAsync(bundledWinSw, "placeholder");

        var manifest = new ServiceManifest
        {
            Id = "sample-service",
            Type = "managed",
            DisplayName = "Sample",
            Runtime = new RuntimeConfig
            {
                WorkDir = temp.Path,
                Executable = Path.Combine(temp.Path, "app.cmd")
            },
            Service = new ServiceConfig
            {
                WrapperDir = wrapperDir,
                StartMode = "manual"
            },
            Health = new HealthConfig
            {
                Url = "http://127.0.0.1/health",
                TimeoutSec = 5
            }
        };

        var manager = new WinSwServiceManager();

        await Assert.ThrowsAnyAsync<Exception>(() => manager.InstallAsync(manifest, "<service></service>"));

        var wrapperExe = Path.Combine(wrapperDir, "sample-service-service.exe");
        Assert.True(File.Exists(wrapperExe), "wrapper executable should be auto-created from winsw.exe candidate");
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
