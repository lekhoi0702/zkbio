using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ZkbioDashboard.Helpers;
using ZkbioDashboard.Models;
using ZkbioDashboard.Services;
using Microsoft.Extensions.Caching.Memory;

namespace ZkbioDashboard.Pages;

public class AttendanceReportModel : PageModel
{
    private readonly ITransactionService _transactionService;
    private readonly IMemoryCache _cache;

    public AttendanceReportModel(ITransactionService transactionService, IMemoryCache cache)
    {
        _transactionService = transactionService;
        _cache = cache;
    }

    public IEnumerable<AttendanceRecord>? Records { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ReportDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Pin { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Factory { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? BU { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<string> SelectedTypes { get; set; } = new();

    public List<string> Types { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public List<string> Factories { get; set; } = new();
    public List<string> BUs { get; set; } = new();

    public async Task OnGetAsync()
    {
        Factories = AttendanceOptions.Factories.ToList();
        BUs = AttendanceOptions.BUs.ToList();
        Types = AttendanceOptions.Types.ToList();
        ReportDate ??= DateTime.Today.AddDays(-1);

        try
        {
            // Cache heavy DB fetch (raw data for the day) for 5 minutes
            string rawCacheKey = CacheKeyHelper.AttendanceRaw(ReportDate.Value, Pin);
            if (!_cache.TryGetValue(rawCacheKey, out List<AttendanceRecord>? allRecords) || allRecords == null)
            {
                var fetchedRecords = await _transactionService.GetAttendanceReportAsync(ReportDate.Value, Pin);
                allRecords = fetchedRecords.ToList();
                _cache.Set(rawCacheKey, allRecords, TimeSpan.FromMinutes(5));
            }

            // Only care about records with at least one effective exception code
            var allExceptions = allRecords
                .Where(r => AttendanceFilterHelper.ComputeEffectiveCodes(r).Any())
                .ToList();
            // Types are now static from AttendanceOptions.Types

            // Cache filtered list for pagination speed (2 mins)
            string filterKey = CacheKeyHelper.AttendanceFiltered(
                ReportDate.Value,
                Pin,
                Factory,
                BU,
                SelectedTypes);
            
            if (!_cache.TryGetValue(filterKey, out List<AttendanceRecord>? filteredList) || filteredList == null)
            {
                filteredList = AttendanceFilterHelper
                    .ApplyFilters(allExceptions, Factory, BU, SelectedTypes)
                    .ToList();
                _cache.Set(filterKey, filteredList, TimeSpan.FromMinutes(2));
            }
            
            TotalCount = filteredList.Count;

            // Apply Paging
            Records = filteredList
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }
        catch (Exception)
        {
            Records = null;
            TotalCount = 0;
        }
    }

    public async Task<JsonResult> OnGetDetailsAsync(string pin, string start, string end)
    {
        if (DateTime.TryParse(start, out var startTime) && DateTime.TryParse(end, out var endTime))
        {
            var logs = await _transactionService.GetTransactionsByRangeAsync(pin, startTime, endTime);
            return new JsonResult(logs);
        }
        return new JsonResult(new List<AccTransaction>());
    }

    public string GetTypeLabel(string type)
    {
        return AttendanceFilterHelper.GetTypeLabel(type);
    }
}

