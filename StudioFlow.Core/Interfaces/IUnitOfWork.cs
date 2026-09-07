namespace StudioFlow.Core.Interfaces;

/// <summary>
/// Owns the single commit point for a request. Repositories only stage changes
/// (Add / Remove / mutate tracked entities); the service layer calls
/// <see cref="SaveChangesAsync"/> once so a multi-entity operation — e.g. bump
/// Class.RegisteredCount and insert a Registration — is written in one database
/// transaction, and an optimistic-concurrency clash surfaces as
/// <c>DbUpdateConcurrencyException</c> (StudioFlow spec §16, §17).
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
