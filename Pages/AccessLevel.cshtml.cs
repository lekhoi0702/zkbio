using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using ZkbioDashboard.Models;
using ZkbioDashboard.Services;

namespace ZkbioDashboard.Pages;

public class AccessLevelModel : PageModel
{
    private readonly ITransactionService _transactionService;
    private readonly IMemoryCache _cache;

    public AccessLevelModel(ITransactionService transactionService, IMemoryCache cache)
    {
        _transactionService = transactionService;
        _cache = cache;
    }

    [BindProperty(SupportsGet = true)]
    public string? Pin { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? AccessLevel { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Department { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FullName { get; set; }

    public IReadOnlyList<AccessLevelRecord> Records { get; private set; } = Array.Empty<AccessLevelRecord>();

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
    public int TotalCount { get; private set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public async Task OnGetAsync()
    {
        ViewData["StartLoading"] = true;
        string cacheKey = $"AccessLevel_{Pin ?? ""}_{AccessLevel ?? ""}_{Department ?? ""}_{FullName ?? ""}";
        if (!_cache.TryGetValue(cacheKey, out List<AccessLevelRecord>? cached) || cached == null)
        {
            var data = await _transactionService.GetAccessLevelsAsync(Pin);
            cached = data.ToList();
            _cache.Set(cacheKey, cached, TimeSpan.FromMinutes(30));
        }

        var filtered = ApplyFilters(cached, AccessLevel, Department, FullName);
        TotalCount = filtered.Count;
        Records = filtered
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }

    public async Task<JsonResult> OnGetDataAsync()
    {
        string cacheKey = $"AccessLevel_{Pin ?? ""}_{AccessLevel ?? ""}_{Department ?? ""}_{FullName ?? ""}";
        if (!_cache.TryGetValue(cacheKey, out List<AccessLevelRecord>? cached) || cached == null)
        {
            var data = await _transactionService.GetAccessLevelsAsync(Pin);
            cached = data.ToList();
            _cache.Set(cacheKey, cached, TimeSpan.FromMinutes(30));
        }

        var filtered = ApplyFilters(cached, AccessLevel, Department, FullName);
        TotalCount = filtered.Count;

        var paged = filtered
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        return new JsonResult(new
        {
            data = paged.Select(x => new
            {
                x.Pin,
                x.FullName,
                DepartmentList = x.DepartmentList,
                AccessLevelList = x.AccessLevelList
            }),
            totalCount = TotalCount,
            pageNumber = PageNumber,
            totalPages = TotalPages
        });
    }

    private static IReadOnlyList<AccessLevelRecord> ApplyFilters(
        IReadOnlyList<AccessLevelRecord> records,
        string? accessLevel,
        string? department,
        string? fullName)
    {
        IEnumerable<AccessLevelRecord> query = records;

        if (!string.IsNullOrWhiteSpace(accessLevel))
        {
            var term = accessLevel.Trim();
            query = query.Where(r => r.AccessLevels.Any(a =>
                a.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var term = department.Trim();
            query = query.Where(r => r.Departments.Any(d =>
                d.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            var term = fullName.Trim();
            query = query.Where(r => r.FullName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return query.ToList();
    }
}
