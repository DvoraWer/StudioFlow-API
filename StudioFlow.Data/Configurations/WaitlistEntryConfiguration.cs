using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioFlow.Core.Entities;

namespace StudioFlow.Data.Configurations;

public class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.JoinedAt)
            .IsRequired();

        builder.Property(w => w.Position)
            .IsRequired();

        builder.Property(w => w.Status)
            .IsRequired();

        // User 1 ---- N WaitlistEntry (Member is a User whose role is Member).
        builder.HasOne(w => w.Member)
            .WithMany(u => u.WaitlistEntries)
            .HasForeignKey(w => w.MemberId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Class 1 ---- N WaitlistEntry.
        builder.HasOne(w => w.Class)
            .WithMany(c => c.WaitlistEntries)
            .HasForeignKey(w => w.ClassId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
