using CvdInbox.Data;
using CvdInbox.Inbox;

namespace CvdInbox.Tests;

public class ReportWorkflowTests
{
    [Theory]
    [InlineData(ReportStatus.New, new[] { ReportStatus.Triage, ReportStatus.Rejected })]
    [InlineData(ReportStatus.Triage, new[] { ReportStatus.Accepted, ReportStatus.Rejected })]
    [InlineData(ReportStatus.Accepted, new[] { ReportStatus.Fixed, ReportStatus.Rejected })]
    [InlineData(ReportStatus.Fixed, new[] { ReportStatus.Disclosed })]
    [InlineData(ReportStatus.Rejected, new[] { ReportStatus.Triage })]
    public void Each_status_has_its_next_steps(ReportStatus from, ReportStatus[] next) =>
        Assert.Equal(next, ReportWorkflow.Next(from));

    [Fact]
    public void A_disclosed_report_goes_nowhere() =>
        Assert.Empty(ReportWorkflow.Next(ReportStatus.Disclosed));

    [Fact]
    public void Only_rejected_and_disclosed_reports_are_closed() =>
        Assert.Equal(
            new[] { ReportStatus.Rejected, ReportStatus.Disclosed },
            Enum.GetValues<ReportStatus>().Where(ReportWorkflow.IsClosed));
}
