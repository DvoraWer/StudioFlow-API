using Microsoft.EntityFrameworkCore;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Data.Context;

namespace StudioFlow.Data.Repositories;

/// <inheritdoc cref="IInstructorRepository" />
public class InstructorRepository : IInstructorRepository
{
    private readonly StudioFlowDbContext _dbContext;

    public InstructorRepository(StudioFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Instructor?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _dbContext.Instructors
            .AsNoTracking()
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<Instructor?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        _dbContext.Instructors
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<Instructor?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
        _dbContext.Instructors
            .AsNoTracking()
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Instructor>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Instructors
            .AsNoTracking()
            .Include(i => i.User)
            .OrderBy(i => i.User.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> HasClassesAsync(int instructorId, CancellationToken cancellationToken = default) =>
        _dbContext.Classes.AnyAsync(c => c.InstructorId == instructorId, cancellationToken);

    public void Add(Instructor instructor) => _dbContext.Instructors.Add(instructor);

    public void Remove(Instructor instructor) => _dbContext.Instructors.Remove(instructor);
}
