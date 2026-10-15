namespace CvdInbox.Disclosure;

public sealed class DisclosureOptions
{
    public const string Section = "Disclosure";

    // Public https origin of this inbox, for example https://inbox.example.org. security.txt is built from it,
    // never from the Host header of the request.
    public string PublicOrigin { get; set; } = "";

    // More ways to reach the security team: mailto:, tel: or https: URIs. The report form is always the first contact.
    public List<string> Contact { get; set; } = [];

    // RFC 9116 asks for less than a year ahead. The app refuses to start once it has passed.
    public DateTimeOffset? Expires { get; set; }

    public string PreferredLanguages { get; set; } = "en";

    public int AcknowledgeWithinDays { get; set; } = 5;

    public int DisclosureAfterDays { get; set; } = 90;

    public Uri Origin => new(PublicOrigin.TrimEnd('/') + "/");
}
