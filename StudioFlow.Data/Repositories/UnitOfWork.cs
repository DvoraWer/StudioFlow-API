using Microsoft.EntityFrameworkCore;
using StudioFlow.Core.Exceptions;
using StudioFlow.Core.Interfaces;
using StudioFlow.Data.Context;

namespace StudioFlow.Data.Repositories;

/// <inheritdoc cref="IUnitOfWork" />
public class UnitOfWork : IUnitOfWork
{
    private readonly StudioFlowDbContext _dbContext;

    public UnitOfWork(StudioFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Translate EF Core's optimistic-concurrency failure into a domain
            // exception so the Service and API layers never depend on EF Core
            // (spec §9, §17). This is the last seat being taken by another request.
            throw new ConcurrencyConflictException(ConcurrencyConflictException.DefaultMessage, ex);
        }
    }
}
