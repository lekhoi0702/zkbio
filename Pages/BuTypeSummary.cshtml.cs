using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ZkbioDashboard.Helpers;
using ZkbioDashboard.Models;
using ZkbioDashboard.Services;
using Microsoft.Extensions.Caching.Memory;

namespace ZkbioDashboard.Pages;

public class BuTypeSummaryModel : PageModel
{
    private readonly ITransactionService _transactionService;
    private readonly IMemoryCache _cache;

    public BuTypeSummaryModel(ITransactionService transactionService, IMemoryCache cache)
    {
        _transactionService = transactionService;
        _cache = cache;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime? ReportDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Factory { get; set; }

    public List<string> Factories { get; set; } = new();
    public List<BuTypeSummaryRow> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        Factories = AttendanceOptions.Factories.ToList();
        ReportDate ??= DateTime.Today.AddDays(-1);

        string cacheKey = $"BuTypeSummary_{ReportDate:yyyyMMdd}_{Factory}";
        if (!_cache.TryGetValue(cacheKey, out List<BuTypeSummaryRow>? cachedRows) || cachedRows == null)
        {
            var allRecords = (await _transactionService.GetAttendanceReportAsync(ReportDate.Value)).ToList();
            var filtered = string.IsNullOrEmpty(Factory)
                ? allRecords
                : allRecords.Where(r => AttendanceFilterHelper.IsFactoryMatch(r, Factory)).ToList();

            cachedRows = BuildSummaryRows(filtered);
            _cache.Set(cacheKey, cachedRows, TimeSpan.FromMinutes(5));
        }

        Rows = cachedRows;
    }

    private static List<BuTypeSummaryRow> BuildSummaryRows(IEnumerable<AttendanceRecord> records)
    {
        var rows = new Dictionary<string, BuTypeSummaryRow>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            if (string.IsNullOrEmpty(record.BU))
                continue;

            if (!rows.TryGetValue(record.BU, out var row))
            {
                row = new BuTypeSummaryRow(record.BU);
                rows[record.BU] = row;
            }

            row.TrackAttendance(record.Pin);

            foreach (var code in AttendanceFilterHelper.ComputeEffectiveCodes(record))
            {
                row.Increment(code);
            }
        }

        return rows.Values
            .OrderBy(r => r.BU)
            .ToList();
    }
}

public class BuTypeSummaryRow
{
    public BuTypeSummaryRow(string bu)
    {
        BU = bu;
    }

    private readonly HashSet<string> _attendancePins = new(StringComparer.OrdinalIgnoreCase);

    public string BU { get; }
    public int AttendanceCount => _attendancePins.Count;
    public int B  { get; private set; }
    public int C1 { get; private set; }
    public int C2 { get; private set; }
    public int D1 { get; private set; }
    public int D2 { get; private set; }
    public int Total => B + C1 + C2 + D1 + D2;

    public void TrackAttendance(string? pin)
    {
        if (!string.IsNullOrWhiteSpace(pin))
            _attendancePins.Add(pin.Trim());
    }

    public void Increment(string code)
    {
        switch (code)
        {
            case "B":
                B++;
                break;
            case "C1":
                C1++;
                break;
            case "C2":
                C2++;
                break;
            case "D1":
                D1++;
                break;
            case "D2":
                D2++;
                break;
        }
    }
}

