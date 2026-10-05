using Microsoft.EntityFrameworkCore;
using StudioFlow.Core.DTOs.Classes;
using StudioFlow.Core.DTOs.Common;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Data.Context;

namespace StudioFlow.Data.Repositories;

/// <inheritdoc cref="IClassRepository" />
public class ClassRepository : IClassRepository
{
    private const int MaxPageSize = 100;

    private readonly StudioFlowDbContext _dbContext;

    public ClassRepository(StudioFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<Class>> GetPagedAsync(
        ClassQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var page = parameters.Page < 1 ? 1 : parameters.Page;
        var pageSize = parameters.PageSize switch
        {
            < 1 => 10,
            > MaxPageSize => MaxPageSize,
            _ => parameters.PageSize
        };

        IQueryable<Class> query = _dbContext.Classes
            .AsNoTracking()
            .Include(c => c.Instructor)
                .ThenInclude(i => i.User)
            .Include(c => c.Room);

        // Classes that have already started are not listed (they can no longer be booked).
        // Applied in the database, before Count and Skip/Take, so paging stays correct.
        var now = DateTime.UtcNow;
        query = query.Where(c => c.StartTime > now);

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var pattern = $"%{parameters.Search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, pattern) ||
                EF.Functions.ILike(c.Description, pattern));
        }

        if (parameters.InstructorId is int instructorId)
        {
            query = query.Where(c => c.InstructorId == instructorId);
        }

        if (parameters.RoomId is int roomId)
        {
            query = query.Where(c => c.RoomId == roomId);
        }

        if (parameters.Status is { } status)
        {
            query = query.Where(c => c.Status == status);
        }

        if (parameters.Date is { } date)
        {
            var dayStart = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(c => c.StartTime >= dayStart && c.StartTime < dayEnd);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.StartTime)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Class>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public Task<Class?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _dbContext.Classes
            .AsNoTracking()
            .Include(c => c.Instructor)
                .ThenInclude(i => i.User)
            .Include(c => c.Room)
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Class?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        _dbContext.Classes
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    // Forces "UPDATE Classes SET RegisteredCount = <same value> WHERE Id = .. AND xmin = ..".
    // The value is unchanged, but the UPDATE makes PostgreSQL check the stale xmin and bump it.
    public void MarkForConcurrencyCheck(Class @class) =>
        _dbContext.Entry(@class).Property(c => c.RegisteredCount).IsModified = true;

    public Task<bool> InstructorHasOverlapAsync(
        int instructorId, DateTime startTime, DateTime endTime, int? excludeClassId,
        CancellationToken cancellationToken = default) =>
        _dbContext.Classes.AnyAsync(c =>
            c.InstructorId == instructorId &&
            c.Status == Core.Enums.ClassStatus.Active &&
            (excludeClassId == null || c.Id != excludeClassId) &&
            c.StartTime < endTime && startTime < c.EndTime,
            cancellationToken);

    public Task<bool> RoomHasOverlapAsync(
        int roomId, DateTime startTime, DateTime endTime, int? excludeClassId,
        CancellationToken cancellationToken = default) =>
        _dbContext.Classes.AnyAsync(c =>
            c.RoomId == roomId &&
            c.Status == Core.Enums.ClassStatus.Active &&
            (excludeClassId == null || c.Id != excludeClassId) &&
            c.StartTime < endTime && startTime < c.EndTime,
            cancellationToken);

    public void Add(Class @class) => _dbContext.Classes.Add(@class);
}
