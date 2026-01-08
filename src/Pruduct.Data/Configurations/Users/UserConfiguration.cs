using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pruduct.Data.Models;

namespace Pruduct.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.Username).IsUnique();
        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.HasIndex(x => x.NormalizedUsername).IsUnique();
        builder.Property(x => x.NormalizedEmail).IsRequired();
        builder.Property(x => x.NormalizedUsername).IsRequired();
        builder.Property(x => x.NormalizedName).IsRequired();
        builder.Property(x => x.AvatarUrl).HasMaxLength(512);
    }
}
