using CvdInbox.Data;
using CvdInbox.Hosting;
using CvdInbox.Security;
using Microsoft.EntityFrameworkCore;

namespace CvdInbox.Reports;

public sealed record ReceiptView(string Receipt);

public sealed record StatusQuery(string? Receipt);

public sealed record StatusView(ReportStatus Status, DateTime ReceivedAt, DateTime UpdatedAt);

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReports(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/reports", SubmitAsync).RequireRateLimiting(RateLimits.Reports);
        // The code travels in the body, never in the URL, so it does not end up in proxy logs.
        routes.MapPost("/api/reports/status", StatusAsync).RequireRateLimiting(RateLimits.Lookups);
        return routes;
    }

    private static async Task<IResult> SubmitAsync(
        NewReport input, InboxDb db, TimeProvider time, SecurityEvents events, HttpContext context, CancellationToken cancellationToken)
    {
        var errors = ReportInput.Validate(input, out var fields);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        // People never see the trap field; bots fill it. The check comes after the validation, so a bot gets the
        // answer a valid report gets, and an invalid one gets the same 400 with or without the trap.
        if (!string.IsNullOrEmpty(input.Website))
        {
            events.TrapFilled(SecurityEvents.ClientAddress(context));
            return Created(ReceiptCode.Create());
        }

        var receipt = ReceiptCode.Create();
        var now = time.GetUtcNow().UtcDateTime;
        var report = new Report
        {
            ReceiptHash = ReceiptCode.Hash(ReceiptCode.Normalize(receipt)!),
            Product = fields.Product,
            Version = fields.Version,
            Description = fields.Description,
            Contact = fields.Contact,
            Status = ReportStatus.New,
            ReceivedAt = now,
            UpdatedAt = now,
        };
        db.Reports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
        // No client address here: researchers may want to stay anonymous.
        events.ReportReceived(report.Id);
        return Created(receipt);
    }

    private static async Task<IResult> StatusAsync(StatusQuery query, InboxDb db, CancellationToken cancellationToken)
    {
        var receipt = ReceiptCode.Normalize(query.Receipt);
        if (receipt is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["receipt"] = ["Enter the receipt code you got when you sent the report."],
            });
        }

        var hash = ReceiptCode.Hash(receipt);
        var status = await db.Reports
            .Where(r => r.ReceiptHash == hash)
            .Select(r => new StatusView(r.Status, r.ReceivedAt, r.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
        return status is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "No report matches this receipt code.")
            : Results.Ok(status);
    }

    private static IResult Created(string receipt) =>
        Results.Json(new ReceiptView(receipt), statusCode: StatusCodes.Status201Created);
}
