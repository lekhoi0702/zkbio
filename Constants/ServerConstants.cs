namespace ZkbioDashboard.Constants;

/// <summary>
/// Centralized server IP patterns and root department name mappings.
/// </summary>
public static class ServerConstants
{
    public const string Server045IpPattern = "0.45";
    public const string Server345IpPattern = "3.45";
    public const string Server046IpPattern = "0.46";

    /// <summary>
    /// Root department names for the Exception Times (Attendance) report.
    /// JSG SHM is included here and consolidated into JSG.
    /// </summary>
    public static string[] GetAttendanceRootNames(string serverIp) => serverIp switch
    {
        var ip when ip.Contains(Server045IpPattern) => ["JIAHSIN", "PD1", "JSG"],
        var ip when ip.Contains(Server345IpPattern) => ["SHIMMER", "JSGS", "JSG SHM"],
        var ip when ip.Contains(Server046IpPattern) => ["JT1", "JT2"],
        _ => []
    };

    /// <summary>
    /// Root department names for Personal and Contractor attendance reports.
    /// JSG SHM is NOT included — it remains a distinct unit here.
    /// </summary>
    public static string[] GetPersonalRootNames(string serverIp) => serverIp switch
    {
        var ip when ip.Contains(Server045IpPattern) => ["JIAHSIN", "PD1", "JSG"],
        var ip when ip.Contains(Server345IpPattern) => ["SHIMMER", "JSGS"],
        var ip when ip.Contains(Server046IpPattern) => ["JT1", "JT2"],
        _ => []
    };
}
