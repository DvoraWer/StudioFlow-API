using Microsoft.EntityFrameworkCore;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Data.Context;

namespace StudioFlow.Data.Repositories;

/// <inheritdoc cref="IUserRepository" />
public class UserRepository : IUserRepository
{
    private readonly StudioFlowDbContext _dbContext;

    public UserRepository(StudioFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        _dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken);

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Users
            .AsNoTracking()
            .OrderBy(u => u.Name)
            .ToListAsync(cancellationToken);

    public void Add(User user) => _dbContext.Users.Add(user);
}
