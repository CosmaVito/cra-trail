namespace CvdInbox.Hosting;

public static class SecurityHeaders
{
    // Angular adds component styles at run time, so styles need 'unsafe-inline'. Scripts come only from this origin.
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
        "font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    // Set when the response starts, so error pages and antiforgery responses get them too.
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Permissions-Policy"] = "camera=(), geolocation=(), microphone=()";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                if (context.Request.Path.StartsWithSegments("/api"))
                    headers.CacheControl = "no-store";
                return Task.CompletedTask;
            });
            return next(context);
        });
}
