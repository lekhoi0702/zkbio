using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ZkbioDashboard.Helpers;
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
    [BindProperty(SupportsGet = true)] public int       ThresholdMinutes   { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public string?   Factory            { get; set; }
    [BindProperty(SupportsGet = true)] public string?   BU                 { get; set; }
    [BindProperty(SupportsGet = true)] public string?   Pin                { get; set; }

    // ---- Results ----
    public IEnumerable<EarlyExitRecord> Records    { get; private set; } = [];
    public List<string>                 Factories  { get; private set; } = [];
    public List<string>                 BUs        { get; private set; } = [];
    
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public async Task OnGetAsync()
    {
        Factories = AttendanceOptions.Factories.ToList();
        BUs = AttendanceOptions.BUs.ToList();
        ReportDate ??= DateTime.Today;
        if (ThresholdMinutes <= 0) ThresholdMinutes = 1;
        if (ThresholdMinutes > 60) ThresholdMinutes = 60;

        string cacheKey = CacheKeyHelper.EarlyExit(
            ReportDate.Value,
            ThresholdMinutes,
            Factory,
            BU,
            Pin);
        
        if (!_cache.TryGetValue(cacheKey, out List<EarlyExitRecord>? allRecords) || allRecords == null)
        {
            var rawRecords = await _transactionService.GetEarlyExitReportAsync(
                ReportDate.Value, ThresholdMinutes, Factory);
            allRecords = rawRecords.ToList();
            _cache.Set(cacheKey, allRecords, TimeSpan.FromMinutes(30));
        }


        var filtered = string.IsNullOrEmpty(Factory)
            ? allRecords
            : allRecords.Where(r =>
                AttendanceFilterHelper.IsFactoryMatch(r.Factory, r.FactoryCluster, Factory)).ToList();

        if (!string.IsNullOrEmpty(BU))
        {
            filtered = filtered
                .Where(r => AttendanceFilterHelper.IsBUMatch(r.BU, r.FactoryCluster, Factory, BU))
                .ToList();
        }

        if (!string.IsNullOrEmpty(Pin))
        {
            var pinFilter = Pin.Trim();
            filtered = filtered
                .Where(r => r.Pin != null && r.Pin.Contains(pinFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

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

        // Fetch all transactions for that PIN (00:00 selected day -> 06:00 next day)
        var start = parsedDate.Date;
        var end   = parsedDate.Date.AddDays(1).AddHours(6);
        var records = await _transactionService.GetTransactionsByRangeAsync(pin, start, end);
        return new JsonResult(records);
    }
}

