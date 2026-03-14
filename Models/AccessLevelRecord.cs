namespace ZkbioDashboard.Models;

public sealed class AccessLevelRecord
{
    public string Pin { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public IReadOnlyList<string> Departments { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AccessLevels { get; init; } = Array.Empty<string>();

    public string DepartmentList => string.Join(", ", Departments);
    public string AccessLevelList => string.Join(", ", AccessLevels);
}
