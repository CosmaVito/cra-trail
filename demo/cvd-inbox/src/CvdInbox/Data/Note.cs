namespace CvdInbox.Data;

public sealed class Note
{
    public int Id { get; set; }

    public int ReportId { get; set; }

    public required string Author { get; set; }

    public required string Text { get; set; }

    public DateTime CreatedAt { get; set; }
}
