namespace Winsvc.Contracts;

public enum ServiceState
{
    Unknown,
    Stopped,
    Starting,
    Running,
    Stopping,
    NotFound
}

public enum HealthState
{
    Unknown,
    Healthy,
    Unhealthy
}

public class WindowsServiceInfo
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ServiceState State { get; set; }
    public string StartMode { get; set; } = string.Empty;
}
