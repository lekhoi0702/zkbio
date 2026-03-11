using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ZkbioDashboard.Models;
using ZkbioDashboard.Services;
using Microsoft.Extensions.Caching.Memory;

namespace ZkbioDashboard.Pages;

public class AttendanceReportModel : PageModel
{
    private readonly ITransactionService _transactionService;
    private readonly IMemoryCache _cache;
    private static readonly List<string> FactoryOptions = ["JIAHSIN", "JSG", "JT1", "JT2", "PD1", "SHIMMER"];
    private static readonly List<string> BUOptions = ["JSG", "BU1", "BU2", "BU3", "PD1", "SHIMMER", "JT1", "JT2"];

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
        Factories = FactoryOptions.ToList();
        BUs = BUOptions.ToList();
        Types = new List<string>();

        // Default to yesterday if not specified
        if (!ReportDate.HasValue)
        {
            ReportDate = DateTime.Today.AddDays(-1);
        }

        try
        {
            // Cache heavy DB fetch (raw data for the day) for 5 minutes
            string rawCacheKey = $"Attendance_Raw_{ReportDate.Value:yyyyMMdd}_{Pin}";
            if (!_cache.TryGetValue(rawCacheKey, out List<AttendanceRecord>? allRecords) || allRecords == null)
            {
                var fetchedRecords = await _transactionService.GetAttendanceReportAsync(ReportDate.Value, Pin);
                allRecords = fetchedRecords.ToList();
                _cache.Set(rawCacheKey, allRecords, TimeSpan.FromMinutes(5));
            }

            // Only care about records with exceptions/errors for this page
            var allExceptions = allRecords.Where(r => !string.IsNullOrEmpty(r.Evaluation)).ToList();
            
            Types = allRecords
                .Select(r => r.Evaluation)
                .Where(t => !string.IsNullOrEmpty(t))
                .SelectMany(t => t.Split(new[] { ',', '[', ']' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(t => t.Trim())
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            // Cache filtered list for pagination speed (2 mins)
            string typesStr = SelectedTypes != null ? string.Join("-", SelectedTypes) : "";
            string filterKey = $"Attendance_Filtered_{ReportDate.Value:yyyyMMdd}_{Pin}_{Factory}_{BU}_{typesStr}";
            
            if (!_cache.TryGetValue(filterKey, out List<AttendanceRecord>? filteredList) || filteredList == null)
            {
                var filtered = allExceptions.AsEnumerable();
                
                if (!string.IsNullOrEmpty(Factory))
                {
                    filtered = filtered.Where(r => IsFactoryMatch(r, Factory));
                }
                if (!string.IsNullOrEmpty(BU))
                    filtered = filtered.Where(r => IsBUMatch(r, Factory, BU));
                if (SelectedTypes != null && SelectedTypes.Any())
                {
                    filtered = filtered.Where(r => r.Evaluation.Split(new[] { ',', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(s => s.Trim())
                                    .Any(code => SelectedTypes.Contains(code)));
                }
                filteredList = filtered.ToList();
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

    private static bool IsFactoryMatch(AttendanceRecord record, string selectedFactory)
    {
        return selectedFactory switch
        {
            "JIAHSIN" => record.Factory == "JIAHSIN" ||
                         (record.Factory == "JSG" && record.FactoryCluster == "JIAHSIN"),
            "SHIMMER" => record.Factory == "SHIMMER" ||
                         (record.Factory == "JSG" && record.FactoryCluster == "SHIMMER"),
            _ => record.Factory == selectedFactory
        };
    }

    private static bool IsBUMatch(AttendanceRecord record, string? selectedFactory, string selectedBU)
    {
        if (selectedBU == "JSG" && selectedFactory == "JIAHSIN")
            return record.BU == "JSG" && record.FactoryCluster == "JIAHSIN";

        if (selectedBU == "JSG" && selectedFactory == "SHIMMER")
            return record.BU == "JSG" && record.FactoryCluster == "SHIMMER";

        return record.BU == selectedBU;
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
        return type switch
        {
            "B" => "[B] Missing the required 2+2 clock-in/clock-out records",
            "C1" => "[C1] Gate Entry 30 Minutes Early",
            "C2" => "[C2] Gate Exit > 30 minutes after Attend Out",
            "D1" => "[D1] Late Arrival",
            "D2" => "[D2] Early Departure",
            _ => type
        };
    }
}
