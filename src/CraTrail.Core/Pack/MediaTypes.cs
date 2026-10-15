namespace CraTrail.Core.Pack;

public static class MediaTypes
{
    public static string For(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.EndsWith(".cdx.json", StringComparison.OrdinalIgnoreCase))
            return "application/vnd.cyclonedx+json";
        if (path.EndsWith(".sarif", StringComparison.OrdinalIgnoreCase))
            return "application/sarif+json";
        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return "application/json";
        if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return "text/markdown";
        if (path.EndsWith(".trx", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            return "application/xml";
        if (path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            return "text/plain";
        return "application/octet-stream";
    }
}
