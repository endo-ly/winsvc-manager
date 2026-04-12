using System.Collections.Generic;
using Winsvc.Contracts.Manifest;

namespace Winsvc.Core;

public class ManifestValidator : IManifestValidator
{
    public IEnumerable<string> Validate(ServiceManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id))
            yield return "Id is required.";

        if (string.IsNullOrWhiteSpace(manifest.DisplayName))
            yield return "DisplayName is required.";
            
        if (manifest.Runtime == null)
            yield return "Runtime configuration is required.";
        else
        {
            if (string.IsNullOrWhiteSpace(manifest.Runtime.WorkDir))
                yield return "Runtime WorkDir is required.";

            if (string.IsNullOrWhiteSpace(manifest.Runtime.Executable))
                yield return "Runtime Executable is required.";
        }
        
        if (manifest.Service == null)
            yield return "Service configuration is required.";
        else if (string.IsNullOrWhiteSpace(manifest.Service.WrapperDir))
            yield return "Service WrapperDir is required.";

        if (manifest.Health == null)
        {
            yield return "Health configuration is required.";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(manifest.Health.Url))
                yield return "Health Url is required.";

            if (manifest.Health.TimeoutSec <= 0)
                yield return "Health TimeoutSec must be greater than 0.";
        }

        if (manifest.Exposure?.TailscaleServe?.Enabled == true)
        {
            if (manifest.Exposure.TailscaleServe.HttpsPort <= 0)
                yield return "TailscaleServe HttpsPort must be greater than 0 when enabled.";

            if (string.IsNullOrWhiteSpace(manifest.Exposure.TailscaleServe.Target))
                yield return "TailscaleServe Target is required when enabled.";
        }
    }
}
