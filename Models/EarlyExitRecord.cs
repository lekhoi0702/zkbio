namespace ZkbioDashboard.Models;

/// <summary>
/// Represents an employee who clocked in on the attendance machine and then
/// immediately exited through the gate within a configurable window (default: 5 minutes).
/// This is used to detect "clocked-in-and-ran-to-parking" behaviour.
/// </summary>
public class EarlyExitRecord
{
    public string Pin         { get; set; } = "";
    public string FullName    { get; set; } = "";
    public string DeptName    { get; set; } = "";
    public string Factory     { get; set; } = "";
    public string FactoryCluster { get; set; } = "";
    public string BU          { get; set; } = "";
    public DateTime Date      { get; set; }
    public string  Shift      { get; set; } = "";

    /// <summary>Time the employee clocked IN on the attendance machine (ATT device).</summary>
    public DateTime? AttendIn     { get; set; }

    /// <summary>The very first gate-exit punch AFTER AttendIn (ACS OUT device).</summary>
    public DateTime? FirstGateOut { get; set; }

    /// <summary>Gap in minutes between AttendIn and FirstGateOut.</summary>
    public double? GapMinutes => AttendIn.HasValue && FirstGateOut.HasValue
        ? (FirstGateOut.Value - AttendIn.Value).TotalMinutes
        : null;
}

