using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioFlow.Core.Entities;

namespace StudioFlow.Data.Configurations;

public class RegistrationConfiguration : IEntityTypeConfiguration<Registration>
{
    public void Configure(EntityTypeBuilder<Registration> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RegisteredAt)
            .IsRequired();

        builder.Property(r => r.Status)
            .IsRequired();

        // User 1 ---- N Registration (Member is a User whose role is Member).
        builder.HasOne(r => r.Member)
            .WithMany(u => u.Registrations)
            .HasForeignKey(r => r.MemberId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Class 1 ---- N Registration.
        builder.HasOne(r => r.Class)
            .WithMany(c => c.Registrations)
            .HasForeignKey(r => r.ClassId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Prevents duplicate registrations for the same member + class (spec §10, §25).
        builder.HasIndex(r => new { r.MemberId, r.ClassId })
            .IsUnique();
    }
}
