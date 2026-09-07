using Microsoft.EntityFrameworkCore;
using StudioFlow.Core.Entities;

namespace StudioFlow.Data.Context;

public class StudioFlowDbContext : DbContext
{
    public StudioFlowDbContext(DbContextOptions<StudioFlowDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Instructor> Instructors => Set<Instructor>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Picks up every IEntityTypeConfiguration<T> in this assembly
        // (Configurations/ folder) — Fluent API only, no data annotations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StudioFlowDbContext).Assembly);
    }
}
