using System.Globalization;

namespace CraTrail.Core.Json;

public static class Timestamps
{
    public static string Format(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
