using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using CvdInbox.Data;
using CvdInbox.Reports;

namespace CvdInbox.Tests;

internal static class TestData
{
    public static readonly object NewReport = new
    {
        product = "Example Product",
        version = "1.2.3",
        description = "Stored XSS in the comment field of the admin page.",
        contact = "researcher@example.test",
        website = "",
    };

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

    public static async Task<string> SubmitAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/reports", NewReport);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReceiptView>(TestJson.Options))!.Receipt;
    }
}
