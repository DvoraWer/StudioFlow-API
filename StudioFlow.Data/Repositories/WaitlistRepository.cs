using Microsoft.EntityFrameworkCore;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Data.Context;

namespace StudioFlow.Data.Repositories;

/// <inheritdoc cref="IWaitlistRepository" />
public class WaitlistRepository : IWaitlistRepository
{
    private readonly StudioFlowDbContext _dbContext;

    public WaitlistRepository(StudioFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsAsync(int memberId, int classId, CancellationToken cancellationToken = default) =>
        _dbContext.WaitlistEntries.AnyAsync(w => w.MemberId == memberId && w.ClassId == classId, cancellationToken);

    public Task<WaitlistEntry?> GetForUpdateAsync(int memberId, int classId, CancellationToken cancellationToken = default) =>
        _dbContext.WaitlistEntries
            .FirstOrDefaultAsync(w => w.MemberId == memberId && w.ClassId == classId, cancellationToken);

    public Task<WaitlistEntry?> GetNextWaitingAsync(int classId, CancellationToken cancellationToken = default) =>
        _dbContext.WaitlistEntries
            .Where(w => w.ClassId == classId && w.Status == WaitlistStatus.Waiting)
            .OrderBy(w => w.Position)
            .ThenBy(w => w.JoinedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> CountWaitingByClassAsync(int classId, CancellationToken cancellationToken = default) =>
        _dbContext.WaitlistEntries
            .CountAsync(w => w.ClassId == classId && w.Status == WaitlistStatus.Waiting, cancellationToken);

    public async Task<IReadOnlyList<WaitlistEntry>> GetByClassAsync(int classId, CancellationToken cancellationToken = default) =>
        await _dbContext.WaitlistEntries
            .AsNoTracking()
            .Include(w => w.Member)
            .Where(w => w.ClassId == classId)
            .OrderBy(w => w.Position)
            .ToListAsync(cancellationToken);

    public void Add(WaitlistEntry entry) => _dbContext.WaitlistEntries.Add(entry);
}
