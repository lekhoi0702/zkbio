using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
    public DateTime? ReportDate { get; set; }

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

    public async Task OnGetAsync()
    {
        if (!ReportDate.HasValue) ReportDate = DateTime.Today.AddDays(-1);

        try
        {
            string cacheKey = $"Contractor_Attendance_{ReportDate.Value:yyyyMMdd}_{Pin}";
            if (!_cache.TryGetValue(cacheKey, out List<AttendanceRecord>? allRecords) || allRecords == null)
            {
                var fetched = await _transactionService.GetContractorsReportAsync(ReportDate.Value, Pin);
                allRecords = fetched.ToList();
                _cache.Set(cacheKey, allRecords, TimeSpan.FromMinutes(5));
            }

            Factories = allRecords
                .Select(r => r.Factory)
                .Where(f => !string.IsNullOrEmpty(f))
                .Distinct()
                .OrderBy(f => f)
                .ToList();

            BUs = allRecords
                .Where(r => string.IsNullOrEmpty(Factory) || r.Factory == Factory)
                .Select(r => r.BU)
                .Where(b => !string.IsNullOrEmpty(b) && !b.Equals("JIAHSIN", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(b => b)
                .ToList();

            Types = allRecords
                .Select(r => r.Evaluation)
                .Where(t => !string.IsNullOrEmpty(t))
                .SelectMany(t => t.Split(new[] { ',', '[', ']' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(t => t.Trim())
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            var filtered = allRecords.AsEnumerable();
            if (!string.IsNullOrEmpty(Factory))
                filtered = filtered.Where(r => r.Factory == Factory);
            if (!string.IsNullOrEmpty(BU))
                filtered = filtered.Where(r => r.BU == BU);
            if (SelectedTypes != null && SelectedTypes.Any())
            {
                filtered = filtered.Where(r => r.Evaluation.Split(new[] { ',', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim())
                                .Any(code => SelectedTypes.Contains(code)));
            }

            Records = filtered.ToList();
        }
        catch (Exception)
        {
            Records = null;
        }
    }

    public string GetTypeLabel(string type)
    {
        return type switch
        {
            "B" => "[B] Missing required 2+2 records",
            "C1" => "[C1] Gate Entry 30m Early",
            "C2" => "[C2] Gate Exit > 30m after Attend Out",
            "D1" => "[D1] Late Arrival",
            "D2" => "[D2] Early Departure",
            _ => type
        };
    }

    public async Task<JsonResult> OnGetDetailsAsync(string pin, string date)
    {
        if (DateTime.TryParse(date, out var d))
        {
            var start = d.Date;
            var end = d.Date.AddDays(1).AddSeconds(-1);
            var logs = await _transactionService.GetTransactionsByRangeAsync(pin, start, end);
            return new JsonResult(logs);
        }
        return new JsonResult(new List<AccTransaction>());
    }
}
