using CvdInbox.Data;

namespace CvdInbox.Inbox;

public static class ReportWorkflow
{
    private static readonly Dictionary<ReportStatus, ReportStatus[]> Moves = new()
    {
        [ReportStatus.New] = [ReportStatus.Triage, ReportStatus.Rejected],
        [ReportStatus.Triage] = [ReportStatus.Accepted, ReportStatus.Rejected],
        [ReportStatus.Accepted] = [ReportStatus.Fixed, ReportStatus.Rejected],
        [ReportStatus.Fixed] = [ReportStatus.Disclosed],
        [ReportStatus.Rejected] = [ReportStatus.Triage],
        [ReportStatus.Disclosed] = [],
    };

    public static IReadOnlyList<ReportStatus> Next(ReportStatus status) => Moves[status];

    public static bool CanMove(ReportStatus from, ReportStatus to) => Moves[from].Contains(to);

    public static bool IsClosed(ReportStatus status) => status is ReportStatus.Rejected or ReportStatus.Disclosed;
}
