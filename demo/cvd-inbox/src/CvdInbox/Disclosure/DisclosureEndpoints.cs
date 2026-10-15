using System.Text;
using Microsoft.Extensions.Options;

namespace CvdInbox.Disclosure;

public sealed record DisclosureView(
    string ReportUrl,
    IReadOnlyList<string> Contact,
    DateTimeOffset Expires,
    string PreferredLanguages,
    int AcknowledgeWithinDays,
    int DisclosureAfterDays,
    int ContactRetentionDays);

public static class DisclosureEndpoints
{
    public static IServiceCollection AddDisclosure(this IServiceCollection services)
    {
        services.AddOptions<DisclosureOptions>()
            .BindConfiguration(DisclosureOptions.Section)
            .Validate(o => IsHttpsOrigin(o.PublicOrigin),
                "Disclosure:PublicOrigin must be an https origin such as https://inbox.example.org.")
            .Validate(o => o.Contact.TrueForAll(IsContactUri),
                "Disclosure:Contact entries must be mailto:, tel: or https: URIs.")
            .Validate<TimeProvider>((o, time) => o.Expires > time.GetUtcNow(),
                "Disclosure:Expires must be a date in the future (RFC 9116).")
            .Validate(o => !string.IsNullOrWhiteSpace(o.PreferredLanguages),
                "Disclosure:PreferredLanguages is required.")
            .Validate(o => o.AcknowledgeWithinDays > 0 && o.DisclosureAfterDays > 0,
                "Disclosure:AcknowledgeWithinDays and Disclosure:DisclosureAfterDays must be positive.")
            .ValidateOnStart();
        return services;
    }

    public static IEndpointRouteBuilder MapDisclosure(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/.well-known/security.txt", (IOptions<DisclosureOptions> options) =>
            Results.Text(SecurityTxt.Render(options.Value), "text/plain", Encoding.UTF8));

        routes.MapGet("/api/disclosure", (IOptions<DisclosureOptions> disclosure, IOptions<InboxOptions> inbox) =>
        {
            var options = disclosure.Value;
            return Results.Ok(new DisclosureView(
                options.Origin.AbsoluteUri,
                options.Contact,
                options.Expires!.Value,
                options.PreferredLanguages,
                options.AcknowledgeWithinDays,
                options.DisclosureAfterDays,
                inbox.Value.ContactRetentionDays));
        });
        return routes;
    }

    private static bool IsHttpsOrigin(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.AbsolutePath == "/"
        && uri.Query.Length == 0
        && uri.Fragment.Length == 0
        && uri.UserInfo.Length == 0;

    private static bool IsContactUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "mailto" or "tel" or "https";
}
