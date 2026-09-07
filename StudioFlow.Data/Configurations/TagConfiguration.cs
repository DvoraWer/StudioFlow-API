using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioFlow.Core.Entities;

namespace StudioFlow.Data.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(50);

        // Tag names are a small controlled vocabulary — no duplicates.
        builder.HasIndex(t => t.Name)
            .IsUnique();

        // Class N ---- N Tag through the implicit "ClassTags" join table
        // (no payload, so no explicit ClassTag entity).
        builder.HasMany(t => t.Classes)
            .WithMany(c => c.Tags)
            .UsingEntity(join => join.ToTable("ClassTags"));
    }
}
