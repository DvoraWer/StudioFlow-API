using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioFlow.Core.Entities;

namespace StudioFlow.Data.Configurations;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.MaximumCapacity)
            .IsRequired();

        builder.Property(r => r.IsActive)
            .IsRequired();

        // Room 1 ---- N Class -> ClassConfiguration.
    }
}
