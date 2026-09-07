using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioFlow.Core.Entities;

namespace StudioFlow.Data.Configurations;

public class ClassConfiguration : IEntityTypeConfiguration<Class>
{
    public void Configure(EntityTypeBuilder<Class> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(c => c.StartTime)
            .IsRequired();

        builder.Property(c => c.EndTime)
            .IsRequired();

        builder.Property(c => c.Capacity)
            .IsRequired();

        builder.Property(c => c.RegisteredCount)
            .IsRequired();

        builder.Property(c => c.Status)
            .IsRequired();

        // Optimistic concurrency (spec §9, §17) — PostgreSQL native approach.
        // The uint Version property is mapped read-only to the system `xmin`
        // column; PostgreSQL bumps it on every UPDATE, so a stale write throws
        // DbUpdateConcurrencyException. No dedicated column is added.
        builder.Property(c => c.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Instructor 1 ---- N Class.
        builder.HasOne(c => c.Instructor)
            .WithMany(i => i.Classes)
            .HasForeignKey(c => c.InstructorId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Room 1 ---- N Class.
        builder.HasOne(c => c.Room)
            .WithMany(r => r.Classes)
            .HasForeignKey(c => c.RoomId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
