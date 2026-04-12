using System.Collections.Generic;

namespace Winsvc.Contracts.Manifest;

public class ServiceManifest
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "managed";
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RuntimeConfig Runtime { get; set; } = new();
    public ServiceConfig Service { get; set; } = new();
    public Dictionary<string, string> Env { get; set; } = new();
    public HealthConfig Health { get; set; } = new();
    public ExposureConfig Exposure { get; set; } = new();
}

public class RuntimeConfig
{
    public string WorkDir { get; set; } = string.Empty;
    public string Executable { get; set; } = string.Empty;
    public List<string> Arguments { get; set; } = new();
}

public class ServiceConfig
{
    public string WrapperDir { get; set; } = string.Empty;
    public string StartMode { get; set; } = "delayed-auto";
    public string OnFailure { get; set; } = "restart";
    public string ResetFailure { get; set; } = "1 hour";
}

public class HealthConfig
{
    public string Url { get; set; } = string.Empty;
    public int TimeoutSec { get; set; } = 5;
}

public class ExposureConfig
{
    public TailscaleServeConfig TailscaleServe { get; set; } = new();
}

public class TailscaleServeConfig
{
    public bool Enabled { get; set; } = false;
    public int HttpsPort { get; set; }
    public string Target { get; set; } = string.Empty;
}
