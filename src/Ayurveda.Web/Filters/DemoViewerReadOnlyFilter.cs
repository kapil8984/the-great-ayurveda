using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ayurveda.Web.Filters;

public class DemoViewerReadOnlyFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;

        if (user.Identity?.IsAuthenticated == true && user.IsInRole("DemoViewer"))
        {
            var method = context.HttpContext.Request.Method.ToUpperInvariant();
            if (method == "POST" || method == "PUT" || method == "DELETE" || method == "PATCH")
            {
                // Allow logging out
                if (path.Contains("/logout", StringComparison.OrdinalIgnoreCase))
                {
                    await next();
                    return;
                }

                var isAjax = string.Equals(context.HttpContext.Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
                    || context.HttpContext.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

                if (isAjax)
                {
                    context.Result = new JsonResult(new
                    {
                        success = false,
                        message = "Read-only demo: modifications are disabled in demo mode."
                    });
                    return;
                }

                if (context.Controller is Controller controller)
                {
                    controller.TempData["ErrorMessage"] = "Read-only demo: modifications are disabled in demo mode.";
                }

                var referer = context.HttpContext.Request.Headers.Referer.ToString();
                if (!string.IsNullOrEmpty(referer) && Uri.TryCreate(referer, UriKind.RelativeOrAbsolute, out var uri))
                {
                    context.Result = new RedirectResult(referer);
                    return;
                }

                context.Result = new RedirectToActionResult("Index", "Dashboard", new { area = "Admin" });
                return;
            }
        }

        await next();
    }
}
