using System.Security.Cryptography;
using CvdInbox.Data;

namespace CvdInbox.Tests;

internal static class TestData
{
    public static Report Report(DateTime receivedAt, string? contact = "researcher@example.test", ReportStatus status = ReportStatus.New) => new()
    {
        ReceiptHash = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)),
        Product = "Example Product",
        Version = "1.2.3",
        Description = "Stored XSS in the comment field of the admin page.",
        Contact = contact,
        Status = status,
        ReceivedAt = receivedAt,
        UpdatedAt = receivedAt,
    };
}
