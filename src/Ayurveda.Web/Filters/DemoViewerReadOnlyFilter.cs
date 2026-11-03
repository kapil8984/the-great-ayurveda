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

                // Allow search operations
                if (path.Contains("/search", StringComparison.OrdinalIgnoreCase) ||
                    (context.ActionDescriptor.RouteValues.TryGetValue("action", out var action) && string.Equals(action, "Search", StringComparison.OrdinalIgnoreCase)))
                {
                    await next();
                    return;
                }

                var isAjax = string.Equals(context.HttpContext.Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
                    || context.HttpContext.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

                context.HttpContext.Response.StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status403Forbidden;
                if (isAjax)
                {
                    context.Result = new JsonResult(new
                    {
                        success = false,
                        message = "Read-only demo: modifications are disabled in demo mode."
                    }) { StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status403Forbidden };
                    return;
                }

                context.Result = new ObjectResult("Forbidden: modifications are disabled in demo mode.")
                {
                    StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status403Forbidden
                };
                return;
            }
        }

        await next();
    }
}
