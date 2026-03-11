namespace ZkbioDashboard.Models;

public class AccTransaction
{
    public DateTime EventTime { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public string DeptCode { get; set; } = string.Empty;
    public string DeptName { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DevAlias { get; set; } = string.Empty;
    public string EventPointName { get; set; } = string.Empty;
    public string EventNo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string VerifyModeNo { get; set; } = string.Empty;
    public string VerifyModeDisplay { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
