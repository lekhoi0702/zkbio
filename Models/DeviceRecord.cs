namespace ZkbioDashboard.Models;

public sealed class DeviceRecord
{
    public string DevAlias { get; init; } = string.Empty;
    public string SN { get; init; } = string.Empty;
    public string AreaName { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public bool IsRegistrationDevice { get; init; }
    public string FwVersion { get; init; } = string.Empty;
}
