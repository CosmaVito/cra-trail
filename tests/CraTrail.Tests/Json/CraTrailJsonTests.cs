using System.Text;
using System.Text.Json;
using CraTrail.Core.Json;

namespace CraTrail.Tests.Json;

public class CraTrailJsonTests
{
    public sealed record Sample(string FirstName, string? Middle);

    [Fact]
    public void Writes_camel_case_two_spaces_lf_and_a_final_newline() =>
        Assert.Equal(
            "{\n  \"firstName\": \"Ada\",\n  \"middle\": null\n}\n",
            Encoding.UTF8.GetString(CraTrailJson.Serialize(new Sample("Ada", null))));

    [Fact]
    public void Missing_or_null_required_properties_are_errors()
    {
        Assert.Throws<JsonException>(() => CraTrailJson.Deserialize<Sample>("""{"firstName":"Ada"}"""u8.ToArray()));
        Assert.Throws<JsonException>(() => CraTrailJson.Deserialize<Sample>("""{"firstName":null,"middle":null}"""u8.ToArray()));
    }

    [Fact]
    public void Timestamps_are_utc_to_the_second() =>
        Assert.Equal("2026-10-15T06:30:00Z", Timestamps.Format(new DateTimeOffset(2026, 10, 15, 8, 30, 0, TimeSpan.FromHours(2))));
}
