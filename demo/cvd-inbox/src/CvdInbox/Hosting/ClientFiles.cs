namespace CvdInbox.Hosting;

public static class ClientFiles
{
    // index.html names the hashed bundles of the current build. Browsers check it again on every visit, so they load the
    // new client after an upgrade. The bundles keep the default caching: a new build gives them new names.
    public static StaticFileOptions Options() => new()
    {
        OnPrepareResponse = context =>
        {
            if (context.File.Name == "index.html")
                context.Context.Response.Headers.CacheControl = "no-cache";
        },
    };
}
