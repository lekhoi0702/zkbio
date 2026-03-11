using ZkbioDashboard.Models;
using Microsoft.Data.SqlClient;

namespace ZkbioDashboard.Helpers;

/// <summary>
/// Static helper for department tree traversal and lookup operations.
/// </summary>
public static class DepartmentHelper
{
    /// <summary>
    /// Returns the server IP string extracted from the connection string DataSource.
    /// </summary>
    public static string GetServerIp(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        return builder.DataSource.Split(',')[0].Trim();
    }

    /// <summary>
    /// Builds a HashSet of allowed department IDs by traversing the department tree
    /// from each root. For PD1, excludes CUS/TMP/SUP sub-departments.
    /// </summary>
    public static HashSet<string> BuildAllowedDepartments(
        List<Department> departments,
        IEnumerable<string> rootNames,
        bool contractorMode = false)
    {
        var allowed = new HashSet<string>();

        foreach (var rootName in rootNames)
        {
            var roots = FindRootDepartments(departments, rootName);

            foreach (var root in roots)
            {
                if (contractorMode)
                {
                    // Contractor mode: only include CUS/TMP/SUP sub-departments
                    AddMatchingDescendants(root.Id, departments, allowed, ["CUS", "TMP", "SUP"]);
                }
                else
                {
                    // Normal mode: exclude CUS/TMP/SUP from PD1
                    string[]? excludes = rootName.Equals("PD1", StringComparison.OrdinalIgnoreCase)
                        ? ["CUS", "TMP", "SUP"]
                        : null;
                    AddDescendants(root.Id, departments, allowed, excludes);
                }
            }
        }

        return allowed;
    }

    /// <summary>
    /// Finds department(s) matching the given root name (by code or name).
    /// JSG, JSGS, and JSG SHM are matched by code only to avoid ambiguity.
    /// </summary>
    public static List<Department> FindRootDepartments(List<Department> departments, string rootName)
    {
        bool codeOnlyMatch = rootName.Equals("JSG", StringComparison.OrdinalIgnoreCase)
                          || rootName.Equals("JSGS", StringComparison.OrdinalIgnoreCase)
                          || rootName.Equals("JSG SHM", StringComparison.OrdinalIgnoreCase);

        return departments.Where(d =>
        {
            bool codeMatch = d.Code != null && d.Code.Trim().Equals(rootName, StringComparison.OrdinalIgnoreCase);
            bool nameMatch = d.Name != null && d.Name.Trim().Equals(rootName, StringComparison.OrdinalIgnoreCase);
            return codeOnlyMatch ? codeMatch : codeMatch || nameMatch;
        }).ToList();
    }

    /// <summary>
    /// Resolves the department ID for a transaction, trying code first then name.
    /// Falls back to a partial name match if an exact name match is not found.
    /// </summary>
    public static string? ResolveDeptId(
        string? deptCode,
        string? deptName,
        Dictionary<string, string> deptMapByCode,
        Dictionary<string, string> deptMapByName,
        List<Department> departments)
    {
        if (deptCode != null && deptMapByCode.TryGetValue(deptCode, out var idByCode))
            return idByCode;

        if (deptName != null)
        {
            var trimmedName = deptName.Trim();
            if (deptMapByName.TryGetValue(trimmedName, out var idByName))
                return idByName;

            // Fallback: partial name match
            var partial = departments.FirstOrDefault(d =>
                d.Name != null && d.Name.Trim().Equals(trimmedName, StringComparison.OrdinalIgnoreCase));
            if (partial != null)
                return partial.Id;
        }

        return null;
    }

    /// <summary>
    /// Walks up the department tree to find the root department that matches one of the target root names.
    /// </summary>
    public static Department? GetRootDepartment(
        string deptId,
        Dictionary<string, Department> deptDict,
        IEnumerable<string> rootNames)
    {
        if (!deptDict.TryGetValue(deptId, out var current))
            return null;

        var root = current;
        while (!string.IsNullOrEmpty(root.ParentId) && deptDict.TryGetValue(root.ParentId, out var parent))
        {
            bool isAlreadyAtRoot = rootNames.Any(rn =>
                (root.Name != null && root.Name.Trim().Equals(rn, StringComparison.OrdinalIgnoreCase)) ||
                (root.Code != null && root.Code.Trim().Equals(rn, StringComparison.OrdinalIgnoreCase)));

            if (isAlreadyAtRoot)
                break;

            root = parent;
        }

        return root;
    }

    /// <summary>
    /// Finds the direct child of the factory root that is an ancestor of the given department.
    /// Used to determine the BU (Business Unit) level.
    /// </summary>
    public static Department? GetBUDepartment(
        string deptId,
        Dictionary<string, Department> deptDict,
        string factoryDeptId)
    {
        if (string.IsNullOrEmpty(factoryDeptId) || !deptDict.TryGetValue(deptId, out var current))
            return null;

        if (current.Id == factoryDeptId)
            return null;

        var node = current;
        while (!string.IsNullOrEmpty(node.ParentId) && deptDict.TryGetValue(node.ParentId, out var parent))
        {
            if (parent.Id == factoryDeptId)
                return node;
            node = parent;
        }

        return null;
    }

    /// <summary>
    /// Recursively adds all descendants of parentId to the allowed set,
    /// optionally excluding subtrees whose code contains any of the excludeSubStrings.
    /// </summary>
    public static void AddDescendants(
        string parentId,
        List<Department> allDepts,
        HashSet<string> allowed,
        string[]? excludeSubStrings = null)
    {
        if (string.IsNullOrEmpty(parentId) || allowed.Contains(parentId))
            return;

        var current = allDepts.FirstOrDefault(d => d.Id == parentId);
        if (current != null && excludeSubStrings != null &&
            excludeSubStrings.Any(s => current.Code != null && current.Code.Contains(s, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        allowed.Add(parentId);
        var children = allDepts.Where(d => d.ParentId == parentId);
        foreach (var child in children)
            AddDescendants(child.Id, allDepts, allowed, excludeSubStrings);
    }

    /// <summary>
    /// Adds only subtrees whose department code contains one of the includeSubStrings.
    /// Used for contractor mode to find CUS/TMP/SUP departments.
    /// </summary>
    private static void AddMatchingDescendants(
        string parentId,
        List<Department> allDepts,
        HashSet<string> allowed,
        string[] includeSubStrings)
    {
        var subtree = new HashSet<string>();
        CollectSubtree(parentId, allDepts, subtree);

        foreach (var dId in subtree)
        {
            var dept = allDepts.FirstOrDefault(d => d.Id == dId);
            if (dept?.Code != null && includeSubStrings.Any(s => dept.Code.Contains(s, StringComparison.OrdinalIgnoreCase)))
            {
                AddDescendants(dId, allDepts, allowed);
            }
        }
    }

    /// <summary>
    /// Recursively collects all IDs in the subtree rooted at parentId.
    /// </summary>
    private static void CollectSubtree(string parentId, List<Department> allDepts, HashSet<string> result)
    {
        if (string.IsNullOrEmpty(parentId) || result.Contains(parentId))
            return;

        result.Add(parentId);
        var children = allDepts.Where(d => d.ParentId == parentId);
        foreach (var child in children)
            CollectSubtree(child.Id, allDepts, result);
    }
}
