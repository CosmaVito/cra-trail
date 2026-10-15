using Microsoft.AspNetCore.Http.Features;

namespace CvdInbox.Hosting;

public static class BodySizeLimit
{
    // A report holds at most 20,000 characters of description; 128 KiB leaves room for any UTF-8 text.
    public const long MaxBytes = 128 * 1024;

    public static IApplicationBuilder UseBodySizeLimit(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            if (context.Request.ContentLength > MaxBytes)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return Task.CompletedTask;
            }
            // Kestrel also stops chunked bodies that grow past the limit.
            if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
                limit.MaxRequestBodySize = MaxBytes;
            return next(context);
        });
}
