using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ZkbioDashboard.Helpers;
using ZkbioDashboard.Models;
using ZkbioDashboard.Services;
using Microsoft.Extensions.Caching.Memory;

namespace ZkbioDashboard.Pages;

public class ContractorAttendanceModel : PageModel
{
    private readonly ITransactionService _transactionService;
    private readonly IMemoryCache _cache;

    public ContractorAttendanceModel(ITransactionService transactionService, IMemoryCache cache)
    {
        _transactionService = transactionService;
        _cache = cache;
    }

    public IEnumerable<AttendanceRecord>? Records { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Pin { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Factory { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? BU { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<string> SelectedTypes { get; set; } = new();

    public List<string> Factories { get; set; } = new();
    public List<string> BUs { get; set; } = new();
    public List<string> Types { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public async Task OnGetAsync()
    {
        Factories = AttendanceOptions.Factories.ToList();
        BUs = AttendanceOptions.BUs.ToList();
        Types = AttendanceOptions.Types.ToList();
        FromDate ??= DateTime.Today.AddDays(-1);
        ToDate ??= DateTime.Today.AddDays(1).AddSeconds(-1);

        if (FromDate > ToDate)
        {
            var temp = FromDate;
            FromDate = ToDate;
            ToDate = temp;
        }

        try
        {
            string cacheKey = CacheKeyHelper.ContractorAttendance(
                FromDate!.Value,
                ToDate!.Value,
                Pin);
            if (!_cache.TryGetValue(cacheKey, out List<AttendanceRecord>? allRecords) || allRecords == null)
            {
                var collected = new List<AttendanceRecord>();
                var from = FromDate!.Value;
                var to = ToDate!.Value;
                var dayCount = (to.Date - from.Date).Days;

                for (int i = 0; i <= dayCount; i++)
                {
                    var day = from.Date.AddDays(i);
                    var fetched = await _transactionService.GetContractorsReportAsync(day, Pin);
                    collected.AddRange(fetched);
                }

                allRecords = collected
                    .Where(r =>
                    {
                        var recordStart = r.FirstPunch ?? r.Date;
                        var recordEnd = r.LastPunch ?? recordStart;
                        return recordEnd >= from && recordStart <= to;
                    })
                    .ToList();
                _cache.Set(cacheKey, allRecords, TimeSpan.FromMinutes(30));
            }
            // Types are now static from AttendanceOptions.Types

            var filtered = allRecords.AsEnumerable();
            if (!string.IsNullOrEmpty(Factory))
                filtered = filtered.Where(r => AttendanceFilterHelper.IsFactoryMatch(r, Factory));
            if (!string.IsNullOrEmpty(BU))
                filtered = filtered.Where(r => AttendanceFilterHelper.IsBUMatch(r, Factory, BU));
            filtered = AttendanceFilterHelper.ApplyTypeFilter(filtered, SelectedTypes);

            var filteredList = filtered.ToList();
            TotalCount = filteredList.Count;
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

    public string GetTypeLabel(string type)
    {
        return AttendanceFilterHelper.GetContractorTypeLabel(type);
    }

    public async Task<JsonResult> OnGetDetailsAsync(string pin, string date)
    {
        if (DateTime.TryParse(date, out var d))
        {
            var start = d.Date.AddHours(4);
            var end = start.AddDays(1).AddHours(2);
            var logs = await _transactionService.GetTransactionsByRangeAsync(pin, start, end);
            return new JsonResult(logs);
        }
        return new JsonResult(new List<AccTransaction>());
    }
}

