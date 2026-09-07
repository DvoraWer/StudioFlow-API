using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioFlow.Core.Entities;

namespace StudioFlow.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(u => u.Role)
            .IsRequired();

        builder.Property(u => u.IsActive)
            .IsRequired();

        // Email must be unique (spec §5).
        builder.HasIndex(u => u.Email)
            .IsUnique();

        // Relationships are configured from the dependent side:
        //   User 1--1 Instructor  -> InstructorConfiguration
        //   User 1--N Registration -> RegistrationConfiguration
        //   User 1--N WaitlistEntry -> WaitlistEntryConfiguration
    }
}
