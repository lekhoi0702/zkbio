using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ZkbioDashboard.Models;
using ZkbioDashboard.Services;

namespace ZkbioDashboard.Pages;

public class PersonalAttendanceModel : PageModel
{
    private readonly ITransactionService _transactionService;

    public PersonalAttendanceModel(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    public IEnumerable<AttendanceRecord>? Records { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Pin { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToDate { get; set; }

    public async Task OnGetAsync()
    {
        if (!FromDate.HasValue) FromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (!ToDate.HasValue) ToDate = DateTime.Today.AddDays(1).AddSeconds(-1);

        if (!string.IsNullOrEmpty(Pin))
        {
            Records = await _transactionService.GetPersonalAttendanceReportAsync(Pin, FromDate.Value, ToDate.Value);
        }
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
