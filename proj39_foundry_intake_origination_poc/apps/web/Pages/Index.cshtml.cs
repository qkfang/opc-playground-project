using Microsoft.AspNetCore.Mvc.RazorPages;
using Proj39.IntakeOrigination.Web.Models;
using Proj39.IntakeOrigination.Web.Services;

namespace Proj39.IntakeOrigination.Web.Pages;

public class IndexModel : PageModel
{
    private readonly FoundryOptions _foundry;
    public IndexModel(FoundryOptions foundry) => _foundry = foundry;

    public string EngineMode => _foundry.IsConfigured ? "foundry" : "offline";

    public void OnGet()
    {
        ViewData["Title"] = "Inbox";
        ViewData["EngineMode"] = EngineMode;
    }
}
