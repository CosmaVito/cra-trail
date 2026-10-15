namespace CvdInbox.Security;

// Security-relevant activity (Annex I, Part I, 2(l)), written to the log under one category.
// Events never carry report text, contacts, receipt codes, notes or passwords.
public sealed partial class SecurityEvents(ILoggerFactory loggerFactory)
{
    public const string Category = "CvdInbox.Security";

    private readonly ILogger logger = loggerFactory.CreateLogger(Category);

    public static string? ClientAddress(HttpContext context) => context.Connection.RemoteIpAddress?.ToString();

    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "Report {ReportId} received.")]
    public partial void ReportReceived(int reportId);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Warning, Message = "Report form trap filled from {ClientAddress}; the report was discarded.")]
    public partial void TrapFilled(string? clientAddress);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Warning, Message = "Rate limit {Policy} reached by {ClientAddress}.")]
    public partial void RateLimited(string policy, string? clientAddress);
}
