using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Services;

namespace WebAppPet.Pages.Business.Explore;

public abstract class ExplorePageModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "dashboard";

    protected void PrepareExplore(string tab)
    {
        Tab = tab;
        if (!BusinessExploreMode.IsActive(Request))
            BusinessExploreMode.Enable(Response);

        ViewData["BusinessExplore"] = true;
        ViewData["LangCompact"] = true;
        ViewData["OnboardShowNotif"] = false;
        ViewData["ExploreTab"] = tab;
        ViewData["ProviderCategories"] = BusinessExploreDemo.CategoryLabels.ToList();
        ViewData["OnboardBackHref"] = Url.Page("./Exit");
    }
}
