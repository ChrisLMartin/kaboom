using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kaboom.Pages;

public sealed class ErrorModel : PageModel
{
    public string? RequestId { get; private set; }

    public void OnGet()
    {
        RequestId = HttpContext.TraceIdentifier;
        _ = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
    }
}
