using Winsvc.Contracts.Manifest;

namespace Winsvc.Core;

public interface IServiceConfigGenerator
{
    string Generate(ServiceManifest manifest);
}
