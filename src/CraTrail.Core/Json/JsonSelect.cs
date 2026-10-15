using System.Text.Json.Nodes;

namespace CraTrail.Core.Json;

public static class JsonSelect
{
    private sealed record Selector(string? Key, string? Value);

    private sealed record Segment(string Name, IReadOnlyList<Selector> Selectors);

    public static IReadOnlyList<JsonNode> Select(JsonNode? root, string path)
    {
        var current = new List<JsonNode>();
        if (root is not null)
            current.Add(root);

        foreach (var segment in Parse(path))
        {
            var next = new List<JsonNode>();
            foreach (var node in current)
            {
                if (JsonAccess.Child(node, segment.Name) is not { } child)
                    continue;
                IEnumerable<JsonNode> values = [child];
                foreach (var selector in segment.Selectors)
                    values = Apply(values, selector);
                next.AddRange(values);
            }
            current = next;
        }
        return current;
    }

    public static bool IsValid(string path)
    {
        try
        {
            Parse(path);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static IEnumerable<JsonNode> Apply(IEnumerable<JsonNode> values, Selector selector)
    {
        foreach (var value in values)
        {
            if (value is JsonArray array)
            {
                foreach (var item in array.OfType<JsonNode>())
                {
                    if (Matches(item, selector))
                        yield return item;
                }
            }
            else if (selector.Key is not null && Matches(value, selector))
            {
                yield return value;
            }
        }
    }

    private static bool Matches(JsonNode node, Selector selector) =>
        selector.Key is null || JsonAccess.Text(JsonAccess.Child(node, selector.Key)) == selector.Value;

    private static List<Segment> Parse(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new FormatException("A path cannot be empty.");

        var segments = new List<Segment>();
        var i = 0;
        while (i < path.Length)
        {
            var start = i;
            while (i < path.Length && path[i] != '.' && path[i] != '[')
                i++;
            var name = path[start..i];
            if (name.Length == 0 || name.Contains(']', StringComparison.Ordinal))
                throw new FormatException($"Invalid segment in path '{path}'.");

            var selectors = new List<Selector>();
            while (i < path.Length && path[i] == '[')
            {
                var close = path.IndexOf(']', i);
                if (close < 0)
                    throw new FormatException($"Missing ']' in path '{path}'.");
                var body = path[(i + 1)..close];
                if (body == "*")
                {
                    selectors.Add(new Selector(null, null));
                }
                else
                {
                    var equals = body.IndexOf('=', StringComparison.Ordinal);
                    if (equals <= 0)
                        throw new FormatException($"Invalid selector '[{body}]' in path '{path}'.");
                    selectors.Add(new Selector(body[..equals], body[(equals + 1)..]));
                }
                i = close + 1;
            }
            segments.Add(new Segment(name, selectors));

            if (i < path.Length)
            {
                if (path[i] != '.')
                    throw new FormatException($"Unexpected '{path[i]}' in path '{path}'.");
                i++;
                if (i == path.Length)
                    throw new FormatException($"Path '{path}' ends with '.'.");
            }
        }
        return segments;
    }
}
