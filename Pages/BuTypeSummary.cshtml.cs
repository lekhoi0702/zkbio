using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ZkbioDashboard.Pages;

public class BuTypeSummaryModel : PageModel
{
    public IActionResult OnGet()
    {
        return Redirect("/abnormal-transactions");
    }
}
