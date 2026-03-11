namespace ZkbioDashboard.Models;

/// <summary>
/// Represents a department node from the auth_department table.
/// </summary>
public class Department
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
}
