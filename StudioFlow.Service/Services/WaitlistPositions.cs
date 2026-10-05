using StudioFlow.Core.Enums;
using StudioFlow.Core.Interfaces.Repositories;

namespace StudioFlow.Service.Services;

/// <summary>
/// Keeps WaitlistEntry.Position as the member's current place in the queue (1, 2, 3, ...).
/// Position is display-only; FIFO promotion order is JoinedAt, then Id.
/// </summary>
internal static class WaitlistPositions
{
    /// <summary>
    /// Renumbers the class's Waiting entries 1..n in FIFO order. Changes are left on the
    /// tracked entities — the caller commits them in its own SaveChangesAsync.
    /// </summary>
    public static async Task ReindexAsync(IWaitlistRepository waitlist, int classId, CancellationToken cancellationToken)
    {
        var entries = await waitlist.GetWaitingByClassForUpdateAsync(classId, cancellationToken);

        // The query reflects the database, so it still returns an entry whose status was
        // changed in this unit of work but not yet saved — skip anything no longer Waiting.
        var position = 1;
        foreach (var entry in entries.Where(e => e.Status == WaitlistStatus.Waiting))
        {
            entry.Position = position++;
        }
    }
}
