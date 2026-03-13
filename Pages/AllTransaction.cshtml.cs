using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ZkbioDashboard.Helpers;
using ZkbioDashboard.Models;
using ZkbioDashboard.Services;

namespace ZkbioDashboard.Pages;

public class AllTransactionModel : PageModel
{
    private readonly ITransactionService _transactionService;
    private static readonly IReadOnlyList<string> FactoryOptions = AttendanceOptions.Factories;

    public AllTransactionModel(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    public IEnumerable<AccTransaction>? Transactions { get; set; }

    [BindProperty(SupportsGet = true)]
    public TransactionFilter Filter { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public List<string> Factories { get; set; } = [];

    public async Task OnGetAsync()
    {
        Factories = FactoryOptions.ToList();

        if (PageNumber < 1) PageNumber = 1;

        if (!Filter.FromDate.HasValue)
            Filter.FromDate = DateTime.Today.AddDays(-3);
        if (!Filter.ToDate.HasValue) 
            Filter.ToDate = DateTime.Today.AddDays(1).AddSeconds(-1);

        try
        {
            var result = await _transactionService.GetTransactionsPagedAsync(PageNumber, PageSize, Filter);
            Transactions = result.Data;
            TotalCount = result.TotalCount;
        }
        catch (Exception)
        {
            Transactions = null;
        }
    }
}


