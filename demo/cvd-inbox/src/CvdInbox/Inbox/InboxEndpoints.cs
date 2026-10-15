using System.Security.Claims;
using CvdInbox.Data;
using CvdInbox.Operators;
using CvdInbox.Reports;
using CvdInbox.Security;
using Microsoft.EntityFrameworkCore;

namespace CvdInbox.Inbox;

public sealed record ReportSummary(int Id, string Product, string Version, ReportStatus Status, DateTime ReceivedAt, DateTime UpdatedAt);

public sealed record NoteView(int Id, string Author, string Text, DateTime CreatedAt);

public sealed record ReportDetail(
    int Id,
    string Product,
    string Version,
    ReportStatus Status,
    DateTime ReceivedAt,
    DateTime UpdatedAt,
    string Description,
    string? Contact,
    DateTime? ContactErasedAt,
    DateTime? ClosedAt,
    IReadOnlyList<ReportStatus> NextStatuses,
    IReadOnlyList<NoteView> Notes);

public sealed record StatusChange(ReportStatus? Status);

public sealed record NewNote(string? Text);

public static class InboxEndpoints
{
    public static IEndpointRouteBuilder MapInbox(this IEndpointRouteBuilder routes)
    {
        var inbox = routes.MapGroup("/api/inbox")
            .RequireAuthorization()
            .AddEndpointFilter<AntiforgeryCheck>();
        inbox.MapGet("", ListAsync);
        inbox.MapGet("/{id:int}", GetAsync);
        inbox.MapPost("/{id:int}/status", ChangeStatusAsync);
        inbox.MapPost("/{id:int}/notes", AddNoteAsync);
        inbox.MapDelete("/{id:int}", DeleteAsync);
        return routes;
    }

    private static async Task<IResult> ListAsync(string? status, InboxDb db, CancellationToken cancellationToken)
    {
        var reports = db.Reports.AsNoTracking();
        if (status is not null)
        {
            if (!Enum.TryParse<ReportStatus>(status, ignoreCase: true, out var filter) || !Enum.IsDefined(filter))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Unknown status."] });
            reports = reports.Where(r => r.Status == filter);
        }

        var list = await reports
            .OrderByDescending(r => r.ReceivedAt).ThenByDescending(r => r.Id)
            .Select(r => new ReportSummary(r.Id, r.Product, r.Version, r.Status, r.ReceivedAt, r.UpdatedAt))
            .ToListAsync(cancellationToken);
        return Results.Ok(list);
    }

    private static async Task<IResult> GetAsync(int id, InboxDb db, CancellationToken cancellationToken)
    {
        var report = await db.Reports.AsNoTracking().Include(r => r.Notes).SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        return report is null ? NoSuchReport() : Results.Ok(Detail(report));
    }

    private static async Task<IResult> ChangeStatusAsync(
        int id, StatusChange change, InboxDb db, TimeProvider time, SecurityEvents events, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (change.Status is not { } target)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Choose the new status."] });

        var report = await db.Reports.Include(r => r.Notes).SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (report is null)
            return NoSuchReport();
        if (!ReportWorkflow.CanMove(report.Status, target))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: $"A report in {report.Status} cannot move to {target}.");

        var now = time.GetUtcNow().UtcDateTime;
        var from = report.Status;
        report.Status = target;
        report.UpdatedAt = now;
        // The contact retention clock starts when the report closes, and stops if it is reopened.
        report.ClosedAt = ReportWorkflow.IsClosed(target) ? now : null;
        await db.SaveChangesAsync(cancellationToken);
        events.StatusChanged(report.Id, from, target, user.Identity!.Name!);
        return Results.Ok(Detail(report));
    }

    private static async Task<IResult> AddNoteAsync(
        int id, NewNote input, InboxDb db, TimeProvider time, SecurityEvents events, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var text = ReportInput.Field(input.Text, "text", 1, ReportInput.NoteMax, multiline: true, errors);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);
        if (!await db.Reports.AnyAsync(r => r.Id == id, cancellationToken))
            return NoSuchReport();

        var note = new Note { ReportId = id, Author = user.Identity!.Name!, Text = text, CreatedAt = time.GetUtcNow().UtcDateTime };
        db.Notes.Add(note);
        await db.SaveChangesAsync(cancellationToken);
        events.NoteAdded(id, note.Author);
        return Results.Json(new NoteView(note.Id, note.Author, note.Text, note.CreatedAt), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> DeleteAsync(int id, InboxDb db, SecurityEvents events, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        // Notes go with the report (ON DELETE CASCADE). secure_delete zeroes the freed space and the WAL
        // is truncated, so no copy stays in the files.
        var deleted = await db.Reports.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0)
            return NoSuchReport();
        // The delete has committed either way. If a reader keeps the log from being truncated, it is logged and retried later.
        if (!await db.TruncateWalAsync(cancellationToken))
            events.WalTruncateIncomplete();
        events.ReportDeleted(id, user.Identity!.Name!);
        return Results.NoContent();
    }

    private static IResult NoSuchReport() =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "No such report.");

    private static ReportDetail Detail(Report report) => new(
        report.Id,
        report.Product,
        report.Version,
        report.Status,
        report.ReceivedAt,
        report.UpdatedAt,
        report.Description,
        report.Contact,
        report.ContactErasedAt,
        report.ClosedAt,
        ReportWorkflow.Next(report.Status),
        [.. report.Notes.OrderBy(n => n.CreatedAt).ThenBy(n => n.Id).Select(n => new NoteView(n.Id, n.Author, n.Text, n.CreatedAt))]);
}
