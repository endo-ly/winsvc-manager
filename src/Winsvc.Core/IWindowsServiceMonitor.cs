using System.Collections.Generic;
using System.Threading.Tasks;
using Winsvc.Contracts;

namespace Winsvc.Core;

public interface IWindowsServiceMonitor
{
    Task<IEnumerable<WindowsServiceInfo>> GetAllServicesAsync();
    Task<WindowsServiceInfo?> GetServiceAsync(string id);
    Task<string?> GetServiceExePathAsync(string id);
}
