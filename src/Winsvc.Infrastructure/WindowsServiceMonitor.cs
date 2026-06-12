using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winsvc.Contracts;
using Winsvc.Core;

namespace Winsvc.Infrastructure;

[SupportedOSPlatform("windows")]
public class WindowsServiceMonitor : IWindowsServiceMonitor
{
    public Task<IEnumerable<WindowsServiceInfo>> GetAllServicesAsync()
    {
        var services = ServiceController.GetServices().Select(MapServiceInfo);
        return Task.FromResult(services);
    }

    public Task<WindowsServiceInfo?> GetServiceAsync(string id)
    {
        try
        {
            var service = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (service == null)
            {
                return Task.FromResult<WindowsServiceInfo?>(null);
            }
            return Task.FromResult<WindowsServiceInfo?>(MapServiceInfo(service));
        }
        catch (Exception)
        {
            return Task.FromResult<WindowsServiceInfo?>(null);
        }
    }

    public Task<string?> GetServiceExePathAsync(string id)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{id}");
            if (key?.GetValue("ImagePath") is string imagePath)
            {
                var exePath = Environment.ExpandEnvironmentVariables(imagePath);
                if (exePath.StartsWith("\"", StringComparison.Ordinal) && exePath.IndexOf('"', 1) is int idx and > 0)
                {
                    exePath = exePath.Substring(1, idx - 1);
                }
                return Task.FromResult<string?>(exePath);
            }
        }
        catch { }

        return Task.FromResult<string?>(null);
    }

    private WindowsServiceInfo MapServiceInfo(ServiceController sc)
    {
        string startMode = "Unknown";
        try { startMode = sc.StartType.ToString(); } catch { }

        return new WindowsServiceInfo
        {
            Id = sc.ServiceName,
            DisplayName = sc.DisplayName,
            State = MapState(sc.Status),
            StartMode = startMode
        };
    }

    private ServiceState MapState(ServiceControllerStatus status)
    {
        return status switch
        {
            ServiceControllerStatus.Stopped => ServiceState.Stopped,
            ServiceControllerStatus.StartPending => ServiceState.Starting,
            ServiceControllerStatus.Running => ServiceState.Running,
            ServiceControllerStatus.StopPending => ServiceState.Stopping,
            _ => ServiceState.Unknown
        };
    }
}
