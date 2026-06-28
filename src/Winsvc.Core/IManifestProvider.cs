using Winsvc.Contracts.Manifest;

namespace Winsvc.Core;

public interface IManifestProvider
{
    Task<IReadOnlyList<ServiceManifest>> GetManifestsAsync(CancellationToken cancellationToken = default);
    Task<ServiceManifest?> GetManifestAsync(string id, CancellationToken cancellationToken = default);
}
