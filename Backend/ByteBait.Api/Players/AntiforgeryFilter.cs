using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ByteBait.Api.Players;

// Protection against CSRF: every request to a controller that can change data (all but GET, HEAD, OPTIONS and TRACE)
// must send the token from GET /api/account/antiforgery in the X-XSRF-TOKEN header. Also register and login.
// MVC's own AutoValidateAntiforgeryToken needs the view services, which an API doesn't have.
public class AntiforgeryFilter(IAntiforgery antiforgery, ProblemDetailsFactory problemDetailsFactory)
    : IAsyncAuthorizationFilter, IOrderedFilter
{
    // After [Authorize], so a request without login gets 401 and not 400
    public int Order => 1000;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (context.Result is not null
            || HttpMethods.IsGet(method) || HttpMethods.IsHead(method)
            || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            var problem = problemDetailsFactory.CreateProblemDetails(
                context.HttpContext,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid antiforgery token",
                detail: $"Get a token from GET /api/account/antiforgery and send it in the {PlayerAccountExtensions.AntiforgeryHeaderName} header. " +
                    "Get a new one after logging in or out.");

            context.Result = new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" },
            };
        }
    }
}
