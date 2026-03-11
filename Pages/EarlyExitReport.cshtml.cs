using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ZkbioDashboard.Models;
using ZkbioDashboard.Services;
using Microsoft.Extensions.Caching.Memory;

namespace ZkbioDashboard.Pages;

public class EarlyExitReportModel : PageModel
{
    private readonly ITransactionService _transactionService;
    private readonly IMemoryCache _cache;

    public EarlyExitReportModel(ITransactionService transactionService, IMemoryCache cache)
    {
        _transactionService = transactionService;
        _cache = cache;
    }

    // ---- Filter inputs ----
    [BindProperty(SupportsGet = true)] public DateTime? ReportDate         { get; set; }
    [BindProperty(SupportsGet = true)] public int       ThresholdMinutes   { get; set; } = 5;
    [BindProperty(SupportsGet = true)] public string?   Factory            { get; set; }

    // ---- Results ----
    public IEnumerable<EarlyExitRecord> Records    { get; private set; } = [];
    public List<string>                 Factories  { get; private set; } = [];
    
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public async Task OnGetAsync()
    {
        ReportDate ??= DateTime.Today;
        if (ThresholdMinutes <= 0) ThresholdMinutes = 5;
        if (ThresholdMinutes > 60) ThresholdMinutes = 60;

        string cacheKey = $"EarlyExit_{ReportDate.Value:yyyyMMdd}_{ThresholdMinutes}_{Factory}";
        
        if (!_cache.TryGetValue(cacheKey, out List<EarlyExitRecord>? allRecords) || allRecords == null)
        {
            var rawRecords = await _transactionService.GetEarlyExitReportAsync(
                ReportDate.Value, ThresholdMinutes, Factory);
            allRecords = rawRecords.ToList();
            _cache.Set(cacheKey, allRecords, TimeSpan.FromMinutes(5));
        }

        Factories = new List<string> { "JIAHSIN", "JSG", "JT1", "JT2", "PD1", "SHIMMER" };

        var filtered = string.IsNullOrEmpty(Factory)
            ? allRecords
            : allRecords.Where(r => 
                r.Factory == Factory || 
                (Factory == "JIAHSIN" && r.Factory == "JSG")
              ).ToList();

        TotalCount = filtered.Count;

        Records = filtered
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }

    public async Task<IActionResult> OnGetDetailsAsync(string pin, string date)
    {
        if (!DateTime.TryParse(date, out var parsedDate))
            return new JsonResult(Array.Empty<object>());

        // Fetch all transactions for that PIN for the full day window (04:00 → next 04:00)
        var start = parsedDate.Date.AddHours(4);
        var end   = start.AddDays(1);
        var records = await _transactionService.GetTransactionsByRangeAsync(pin, start, end);

        var result = records.Select(t => new
        {
            eventTime         = t.EventTime,
            areaName          = t.AreaName,
            type              = t.Type,
            devAlias          = t.DevAlias,
            eventPointName    = t.EventPointName,
            verifyModeDisplay = t.VerifyModeDisplay,
            status            = t.Status
        });
        return new JsonResult(result);
    }
}
