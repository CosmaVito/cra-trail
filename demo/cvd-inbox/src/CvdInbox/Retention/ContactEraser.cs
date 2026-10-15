using CvdInbox.Data;
using CvdInbox.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CvdInbox.Retention;

// Annex I, Part I, 2(g): the researcher's contact is kept only while the report is open,
// plus Inbox:ContactRetentionDays after it closes.
public sealed class ContactEraser(InboxDb db, TimeProvider time, IOptions<InboxOptions> options, SecurityEvents events)
{
    public async Task<int> EraseExpiredAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var cutoff = now.AddDays(-options.Value.ContactRetentionDays);
        var erased = await db.Reports
            .Where(r => r.Contact != null && r.ClosedAt != null && r.ClosedAt <= cutoff)
            .ExecuteUpdateAsync(set => set
                .SetProperty(r => r.Contact, (string?)null)
                .SetProperty(r => r.ContactErasedAt, now), cancellationToken);
        // Logged once the update has committed; the log truncate comes after it, in a step of its own.
        if (erased > 0)
            events.ContactsErased(erased);
        return erased;
    }

    // A truncate that did not finish is logged; the next sweep tries again.
    public async Task TruncateWalAsync(CancellationToken cancellationToken)
    {
        if (!await db.TruncateWalAsync(cancellationToken))
            events.WalTruncateIncomplete();
    }
}
