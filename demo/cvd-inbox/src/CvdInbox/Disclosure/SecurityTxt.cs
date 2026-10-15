using System.Globalization;
using System.Text;

namespace CvdInbox.Disclosure;

// RFC 9116. Contact and Expires are required; Canonical and Policy point back to this inbox.
public static class SecurityTxt
{
    public static string Render(DisclosureOptions options)
    {
        var origin = options.Origin;
        var text = new StringBuilder();
        Line(text, "Contact", origin.AbsoluteUri);
        foreach (var contact in options.Contact)
            Line(text, "Contact", contact);
        Line(text, "Expires", options.Expires!.Value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        Line(text, "Preferred-Languages", options.PreferredLanguages);
        Line(text, "Canonical", new Uri(origin, ".well-known/security.txt").AbsoluteUri);
        Line(text, "Policy", new Uri(origin, "security-policy").AbsoluteUri);
        return text.ToString();
    }

    private static void Line(StringBuilder text, string field, string value) =>
        text.Append(field).Append(": ").Append(value).Append('\n');
}
