using CvdInbox.Data;

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

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information, Message = "Operator {OperatorName} created from the command line.")]
    public partial void OperatorCreated(string operatorName);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Operator {OperatorName} signed in from {ClientAddress}.")]
    public partial void SignedIn(string operatorName, string? clientAddress);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Failed sign-in as {OperatorName} from {ClientAddress}.")]
    public partial void SignInFailed(string operatorName, string? clientAddress);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Sign-in as {OperatorName} from {ClientAddress} refused: the account is locked.")]
    public partial void LockedOut(string operatorName, string? clientAddress);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "Operator {OperatorName} signed out.")]
    public partial void SignedOut(string operatorName);

    [LoggerMessage(EventId = 1104, Level = LogLevel.Warning, Message = "Request to {Path} from {ClientAddress} refused: missing or invalid request token.")]
    public partial void RequestTokenRejected(string path, string? clientAddress);

    [LoggerMessage(EventId = 1201, Level = LogLevel.Information, Message = "Report {ReportId} moved from {From} to {To} by {OperatorName}.")]
    public partial void StatusChanged(int reportId, ReportStatus from, ReportStatus to, string operatorName);

    [LoggerMessage(EventId = 1202, Level = LogLevel.Information, Message = "Note added to report {ReportId} by {OperatorName}.")]
    public partial void NoteAdded(int reportId, string operatorName);

    [LoggerMessage(EventId = 1203, Level = LogLevel.Warning, Message = "Report {ReportId} and its notes deleted for good by {OperatorName}.")]
    public partial void ReportDeleted(int reportId, string operatorName);
}
