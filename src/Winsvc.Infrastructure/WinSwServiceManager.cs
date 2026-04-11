using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Winsvc.Contracts.Manifest;
using Winsvc.Core;

namespace Winsvc.Infrastructure;

public class WinSwServiceManager : IServiceManager
{
    // C# 9+ Requires WinSW executable to be placed inside WrapperDir as '{id}-service.exe'
    
    public async Task InstallAsync(ServiceManifest manifest, string configContent)
    {
        EnsureManaged(manifest);
        var exePath = GetExePath(manifest);
        var xmlPath = GetXmlPath(manifest);

        Directory.CreateDirectory(manifest.Service.WrapperDir);
        
        // Write XML
        await File.WriteAllTextAsync(xmlPath, configContent);
        
        // We assume WinSW execution file is manually placed or downloaded to exePath, 
        // but if it's missing, we should error out (or download it). For now, we assume it's there.
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"WinSW executable not found at {exePath}. Please place it before installing.");
        }

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
