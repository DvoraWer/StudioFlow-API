using Microsoft.EntityFrameworkCore;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Data.Context;

namespace StudioFlow.Data.Repositories;

/// <inheritdoc cref="IRoomRepository" />
public class RoomRepository : IRoomRepository
{
    private readonly StudioFlowDbContext _dbContext;

    public RoomRepository(StudioFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Room?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _dbContext.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Room?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        _dbContext.Rooms
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Room>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Rooms
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> HasClassesAsync(int roomId, CancellationToken cancellationToken = default) =>
        _dbContext.Classes.AnyAsync(c => c.RoomId == roomId, cancellationToken);

    public void Add(Room room) => _dbContext.Rooms.Add(room);

    public void Remove(Room room) => _dbContext.Rooms.Remove(room);
}
