namespace ZkbioDashboard.Models;

public class AttendanceRecord
{
    public string DeptName { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Factory { get; set; } = string.Empty;
    public string FactoryCluster { get; set; } = string.Empty;
    public string BU { get; set; } = string.Empty;
    public DateTime? GateIn { get; set; }
    public DateTime? AttendIn { get; set; }
    public DateTime? AttendOut { get; set; }
    public DateTime? GateOut { get; set; }
    public DateTime? FirstPunch { get; set; }
    public DateTime? LastPunch { get; set; }
    public DateTime Date { get; set; }
    public string Evaluation { get; set; } = string.Empty;
    public string Shift { get; set; } = string.Empty;
}
