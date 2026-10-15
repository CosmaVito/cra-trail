using CraTrail.Core.Json;

namespace CraTrail.Tests.Json;

public class YamlToJsonTests
{
    [Fact]
    public void Plain_scalars_get_json_types()
    {
        var node = YamlToJson.Convert("""
            text: hello
            number: 9.0
            integer: 0
            flag: true
            nothing: ~
            date: 2031-12-31
            quoted: '7'
            list: [a, b]
            empty: {}
            """)!;

        Assert.Equal(
            """{"text":"hello","number":9,"integer":0,"flag":true,"nothing":null,"date":"2031-12-31","quoted":"7","list":["a","b"],"empty":{}}""",
            node.ToJsonString());
    }

    [Fact]
    public void Duplicate_keys_are_rejected() =>
        Assert.Throws<FormatException>(() => YamlToJson.Convert("a: 1\na: 2"));

    [Fact]
    public void Empty_text_gives_null() => Assert.Null(YamlToJson.Convert(""));
}
