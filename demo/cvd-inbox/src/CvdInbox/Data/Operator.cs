namespace CvdInbox.Data;

public sealed class Operator
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string PasswordHash { get; set; }

    public int FailedSignIns { get; set; }

    public DateTime? LockedUntil { get; set; }

    public DateTime CreatedAt { get; set; }
}
