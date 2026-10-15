namespace CvdInbox.Data;

public sealed class Report
{
    public int Id { get; set; }

    // SHA-256 of the receipt code; the code itself is never stored.
    public required string ReceiptHash { get; set; }

    public required string Product { get; set; }

    public required string Version { get; set; }

    public required string Description { get; set; }

    public string? Contact { get; set; }

    public ReportStatus Status { get; set; }

    public DateTime ReceivedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime? ContactErasedAt { get; set; }

    public List<Note> Notes { get; } = [];
}
