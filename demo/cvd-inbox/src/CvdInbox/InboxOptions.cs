using System.Net;

namespace CvdInbox;

public sealed class InboxOptions
{
    public const string Section = "Inbox";

    // Folder for the database and the data protection keys. A relative path starts at the content root.
    public string DataPath { get; set; } = "data";

    // Days a researcher's contact is kept after the report is closed (rejected or disclosed).
    public int ContactRetentionDays { get; set; } = 30;

    public TimeSpan RetentionSweepInterval { get; set; } = TimeSpan.FromHours(1);

    // Reverse proxies allowed to set X-Forwarded-For. Empty when clients reach the app directly.
    public List<string> TrustedProxies { get; set; } = [];

    public string DataDirectory(IHostEnvironment environment) =>
        Path.GetFullPath(DataPath, environment.ContentRootPath);
}

public static class InboxOptionsSetup
{
    public static IServiceCollection AddInboxOptions(this IServiceCollection services)
    {
        services.AddOptions<InboxOptions>()
            .BindConfiguration(InboxOptions.Section)
            .Validate(o => !string.IsNullOrWhiteSpace(o.DataPath), "Inbox:DataPath is required.")
            .Validate(o => o.ContactRetentionDays >= 0, "Inbox:ContactRetentionDays cannot be negative.")
            .Validate(o => o.RetentionSweepInterval >= TimeSpan.FromMinutes(1) && o.RetentionSweepInterval <= TimeSpan.FromDays(1),
                "Inbox:RetentionSweepInterval must be between one minute and one day.")
            .Validate(o => o.TrustedProxies.TrueForAll(p => IPAddress.TryParse(p, out _)),
                "Inbox:TrustedProxies must list IP addresses.")
            .ValidateOnStart();
        return services;
    }
}
