using System.Net;
using System.Net.Http.Json;
using CvdInbox.Inbox;
using CvdInbox.Reports;

namespace CvdInbox.Tests;

public class LoggingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task No_log_record_carries_report_text_contacts_receipts_notes_or_passwords()
    {
        const string password = "zz-password-5150, long enough";
        await using var app = new InboxApp();
        var researcher = app.CreateBrowser(peer: "203.0.113.9");
        var submitted = await researcher.PostAsJsonAsync("/api/reports", new
        {
            product = "zz-product-5150",
            version = "zz-version-5150",
            description = "zz-description-5150 with enough words to pass",
            contact = "zz-contact-5150@example.test",
            website = "",
        }, Ct);
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var receipt = (await submitted.Content.ReadFromJsonAsync<ReceiptView>(TestJson.Options, Ct))!.Receipt;
        (await researcher.PostAsJsonAsync("/api/reports/status", new { receipt }, Ct)).EnsureSuccessStatusCode();

        var client = await Browser.SignedInAsync(app, password: password);
        // People type passwords into the name field too.
        var mistyped = app.CreateBrowser(peer: "203.0.113.10");
        await Browser.RefreshTokenAsync(mistyped);
        Assert.Equal(HttpStatusCode.Unauthorized, (await mistyped.PostAsJsonAsync("/api/auth/sign-in", new { name = password, password = "x" }, Ct)).StatusCode);
        var id = Assert.Single((await client.GetFromJsonAsync<List<ReportSummary>>("/api/inbox", TestJson.Options, Ct))!).Id;
        (await client.PostAsJsonAsync($"/api/inbox/{id}/status", new { status = "triage" }, Ct)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/inbox/{id}/notes", new { text = "zz-note-5150" }, Ct)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/inbox/{id}", Ct)).StatusCode);

        // Every step was logged, so the check below looks at real records.
        Assert.Superset(new HashSet<int> { 1001, 1002, 1101, 1201, 1202, 1203 }, app.SecurityEvents().Select(e => e.Id.Id).ToHashSet());
        string[] markers = ["zz-product-5150", "zz-version-5150", "zz-description-5150", "zz-contact-5150", "zz-note-5150", "zz-password-5150",
            receipt, ReceiptCode.Normalize(receipt)!];
        foreach (var record in app.Logs())
        {
            string?[] texts = [record.Message, record.Exception?.ToString(), .. record.StructuredState?.Select(pair => pair.Value) ?? []];
            foreach (var marker in markers)
                Assert.DoesNotContain(texts, text => text?.Contains(marker, StringComparison.Ordinal) == true);
        }
    }
}
