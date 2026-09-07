using Microsoft.EntityFrameworkCore;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Data.Context;

namespace StudioFlow.Data.Repositories;

/// <inheritdoc cref="IRegistrationRepository" />
public class RegistrationRepository : IRegistrationRepository
{
    private readonly StudioFlowDbContext _dbContext;

    public RegistrationRepository(StudioFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Registration?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _dbContext.Registrations
            .AsNoTracking()
            .Include(r => r.Class)
            .Include(r => r.Member)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Registration?> GetForUpdateAsync(int memberId, int classId, CancellationToken cancellationToken = default) =>
        _dbContext.Registrations
            .FirstOrDefaultAsync(r => r.MemberId == memberId && r.ClassId == classId, cancellationToken);

    public Task<bool> ExistsAsync(int memberId, int classId, CancellationToken cancellationToken = default) =>
        _dbContext.Registrations.AnyAsync(r => r.MemberId == memberId && r.ClassId == classId, cancellationToken);

    public Task<int> CountActiveByClassAsync(int classId, CancellationToken cancellationToken = default) =>
        _dbContext.Registrations
            .CountAsync(r => r.ClassId == classId && r.Status == RegistrationStatus.Active, cancellationToken);

    public async Task<IReadOnlyList<Registration>> GetByMemberAsync(int memberId, CancellationToken cancellationToken = default) =>
        await _dbContext.Registrations
            .AsNoTracking()
            .Include(r => r.Class)
                .ThenInclude(c => c.Room)
            .Include(r => r.Class)
                .ThenInclude(c => c.Instructor)
                    .ThenInclude(i => i.User)
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.RegisteredAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Registration>> GetActiveByClassAsync(int classId, CancellationToken cancellationToken = default) =>
        await _dbContext.Registrations
            .AsNoTracking()
            .Include(r => r.Member)
            .Where(r => r.ClassId == classId && r.Status == RegistrationStatus.Active)
            .OrderBy(r => r.RegisteredAt)
            .ToListAsync(cancellationToken);

    public void Add(Registration registration) => _dbContext.Registrations.Add(registration);
}
