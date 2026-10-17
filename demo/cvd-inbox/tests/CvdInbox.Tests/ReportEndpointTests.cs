using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CvdInbox.Data;
using CvdInbox.Reports;
using Microsoft.EntityFrameworkCore;

namespace CvdInbox.Tests;

public class ReportEndpointTests
{
    [Fact]
    public async Task A_report_gets_a_receipt_and_only_its_hash_is_stored()
    {
        await using var app = new InboxApp();

        var receipt = await TestData.SubmitAsync(app.CreateBrowser());

        Assert.Matches("^[0-9a-f]{4}(-[0-9a-f]{4}){7}$", receipt);
        var stored = await app.WithDbAsync(db => db.Reports.SingleAsync());
        Assert.Equal(ReceiptCode.Hash(ReceiptCode.Normalize(receipt)!), stored.ReceiptHash);
        Assert.Equal(ReportStatus.New, stored.Status);
        Assert.Equal("researcher@example.test", stored.Contact);
        Assert.Equal(app.Time.GetUtcNow().UtcDateTime, stored.ReceivedAt);
    }

    [Fact]
    public async Task Invalid_fields_come_back_as_a_validation_problem()
    {
        await using var app = new InboxApp();

        var response = await app.CreateBrowser().PostAsJsonAsync("/api/reports",
            new { product = "", version = "1.0", description = "too short", contact = "", website = "" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var fields = problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(new[] { "description", "product" }, fields);
        Assert.Equal(0, await app.WithDbAsync(db => db.Reports.CountAsync()));
    }

    [Fact]
    public async Task A_filled_trap_field_discards_the_report_and_records_the_address()
    {
        await using var app = new InboxApp();

        var response = await app.CreateBrowser(peer: "203.0.113.7").PostAsJsonAsync("/api/reports",
            new { product = "Watches", version = "1", description = "Buy cheap watches at our shop today", contact = "", website = "http://spam.example" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // Shaped like a real receipt, so the bot cannot tell.
        var receipt = (await response.Content.ReadFromJsonAsync<ReceiptView>(TestJson.Options, TestContext.Current.CancellationToken))!.Receipt;
        Assert.Matches("^[0-9a-f]{4}(-[0-9a-f]{4}){7}$", receipt);
        Assert.Equal(0, await app.WithDbAsync(db => db.Reports.CountAsync()));
        var trap = Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1102);
        Assert.Contains("203.0.113.7", trap.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(app.SecurityEvents(), e => e.Id.Id == 1101);
    }

    [Fact]
    public async Task An_invalid_report_gets_the_validation_problem_even_with_the_trap_filled()
    {
        await using var app = new InboxApp();

        var response = await app.CreateBrowser(peer: "203.0.113.7").PostAsJsonAsync("/api/reports",
            new { product = "", version = "1", description = "too short", contact = "", website = "http://spam.example" },
            TestContext.Current.CancellationToken);

        // Otherwise the status code would tell a bot which field is the trap: 201 for an invalid report means it was caught.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(app.SecurityEvents(), e => e.Id.Id == 1102);
    }

    [Fact]
    public async Task Report_logs_never_carry_the_researcher_address_contact_or_receipt()
    {
        await using var app = new InboxApp();
        var client = app.CreateBrowser(peer: "203.0.113.8");

        var receipt = await TestData.SubmitAsync(client);
        await client.PostAsJsonAsync("/api/reports/status", new { receipt }, TestContext.Current.CancellationToken);

        Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1101);
        var everything = string.Join('\n', app.Logs().Select(r => r.Message));
        Assert.DoesNotContain("203.0.113.8", everything, StringComparison.Ordinal);
        Assert.DoesNotContain("researcher@example.test", everything, StringComparison.Ordinal);
        Assert.DoesNotContain(receipt, everything, StringComparison.Ordinal);
        Assert.DoesNotContain(ReceiptCode.Normalize(receipt)!, everything, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Status_lookup_shows_the_state_and_nothing_else()
    {
        await using var app = new InboxApp();
        var client = app.CreateBrowser();
        var receipt = await TestData.SubmitAsync(client);

        var typed = receipt.ToUpperInvariant().Replace("-", " ", StringComparison.Ordinal);
        var response = await client.PostAsJsonAsync("/api/reports/status", new { receipt = typed }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "receivedAt", "status", "updatedAt" }, body.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal("new", body.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef", HttpStatusCode.NotFound)]
    [InlineData("not a receipt", HttpStatusCode.BadRequest)]
    [InlineData("", HttpStatusCode.BadRequest)]
    public async Task Unknown_receipts_get_404_and_malformed_ones_400(string receipt, HttpStatusCode expected)
    {
        await using var app = new InboxApp();

        var response = await app.CreateBrowser().PostAsJsonAsync("/api/reports/status", new { receipt }, TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }
}
