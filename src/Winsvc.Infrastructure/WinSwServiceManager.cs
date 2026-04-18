using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Winsvc.Contracts.Manifest;
using Winsvc.Core;

namespace Winsvc.Infrastructure;

public class WinSwServiceManager : IServiceManager
{
    public async Task InstallAsync(ServiceManifest manifest, string configContent)
    {
        EnsureManaged(manifest);
        var exePath = GetExePath(manifest);
        var xmlPath = GetXmlPath(manifest);

        Directory.CreateDirectory(manifest.Service.WrapperDir);
        EnsureWinSwWrapperExecutable(manifest, exePath);

        // Write XML
        await File.WriteAllTextAsync(xmlPath, configContent);

        await RunCommandAsync(exePath, "install");
    }

    public async Task UninstallAsync(ServiceManifest manifest)
    {
        EnsureManaged(manifest);
        await RunCommandAsync(GetExePath(manifest), "uninstall");
    }

    public async Task StartAsync(ServiceManifest manifest)
    {
        EnsureManaged(manifest);
        await RunCommandAsync(GetExePath(manifest), "start");
    }

    public async Task StopAsync(ServiceManifest manifest)
    {
        EnsureManaged(manifest);
        await RunCommandAsync(GetExePath(manifest), "stop");
    }

    public async Task RestartAsync(ServiceManifest manifest)
    {
        EnsureManaged(manifest);
        await RunCommandAsync(GetExePath(manifest), "restart");
    }

    private void EnsureManaged(ServiceManifest manifest)
    {
        if (manifest.Type != "managed")
            throw new InvalidOperationException($"Service '{manifest.Id}' is of type '{manifest.Type}'. Destructive operations are not allowed.");
    }

    private string GetExePath(ServiceManifest manifest) => Path.Combine(manifest.Service.WrapperDir, $"{manifest.Id}-service.exe");
    private string GetXmlPath(ServiceManifest manifest) => Path.Combine(manifest.Service.WrapperDir, $"{manifest.Id}-service.xml");
    
    private static void EnsureWinSwWrapperExecutable(ServiceManifest manifest, string wrapperExePath)
    {
        if (File.Exists(wrapperExePath))
        {
            return;
        }

        var sourcePath = ResolveWinSwSourcePath(manifest.Service.WrapperDir);
        if (sourcePath is null)
        {
            throw new FileNotFoundException(
                $"WinSW wrapper executable not found at {wrapperExePath}. " +
                "Place '<id>-service.exe' under service.wrapperDir, or place 'winsw.exe' next to winsvc.exe.");
        }

        File.Copy(sourcePath, wrapperExePath, overwrite: false);
    }

    private static string? ResolveWinSwSourcePath(string wrapperDir)
    {
        foreach (var filename in new[] { "winsw.exe", "WinSW.exe", "WinSW-net462.exe" })
        {
            var wrapperCandidate = Path.Combine(wrapperDir, filename);
            if (File.Exists(wrapperCandidate))
            {
                return wrapperCandidate;
            }

            var bundledCandidate = Path.Combine(AppContext.BaseDirectory, filename);
            if (File.Exists(bundledCandidate))
            {
                return bundledCandidate;
            }
        }

        return null;
    }

    private async Task RunCommandAsync(string exePath, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) throw new InvalidOperationException($"Failed to start {exePath}");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var err = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Command '{arguments}' failed with exit code {process.ExitCode}: {err}");
        }
    }
}
