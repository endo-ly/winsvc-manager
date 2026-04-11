using System.Threading.Tasks;
using Winsvc.Contracts;
using Winsvc.Contracts.Manifest;

namespace Winsvc.Core;

public interface IServiceManager
{
    Task InstallAsync(ServiceManifest manifest, string configContent);
    Task UninstallAsync(ServiceManifest manifest);
    Task StartAsync(ServiceManifest manifest);
    Task StopAsync(ServiceManifest manifest);
    Task RestartAsync(ServiceManifest manifest);
}
