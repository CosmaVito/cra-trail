using CvdInbox.Security;
using Microsoft.AspNetCore.Antiforgery;

namespace CvdInbox.Operators;

// UseAntiforgery only checks form posts. The JSON API checks the X-XSRF-TOKEN header here.
public sealed class AntiforgeryCheck(IAntiforgery antiforgery, SecurityEvents events) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method))
            return await next(context);

        if (!await antiforgery.IsRequestValidAsync(http))
        {
            events.RequestTokenRejected(http.Request.Path.Value ?? "", SecurityEvents.ClientAddress(http));
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "The page expired. Reload it and try again.");
        }
        return await next(context);
    }
}
