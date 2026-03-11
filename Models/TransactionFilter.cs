namespace ZkbioDashboard.Models;

public class TransactionFilter
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? AreaName { get; set; }
    public string? DeptName { get; set; }
    public string? Pin { get; set; }
    public string? Name { get; set; }
    public string? DevAlias { get; set; }
    public string? EventPoint { get; set; }
    public string? Status { get; set; }
}
