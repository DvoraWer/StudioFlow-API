using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioFlow.Core.Entities;

namespace StudioFlow.Data.Configurations;

public class InstructorConfiguration : IEntityTypeConfiguration<Instructor>
{
    public void Configure(EntityTypeBuilder<Instructor> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Specialization)
            .HasMaxLength(200);

        builder.Property(i => i.Bio)
            .HasMaxLength(2000);

        // User 1 ---- 1 Instructor. FK on Instructor.UserId; deleting the User
        // removes its Instructor profile.
        builder.HasOne(i => i.User)
            .WithOne(u => u.Instructor)
            .HasForeignKey<Instructor>(i => i.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.UserId)
            .IsUnique();

        // Instructor 1 ---- N Class -> ClassConfiguration.
    }
}
