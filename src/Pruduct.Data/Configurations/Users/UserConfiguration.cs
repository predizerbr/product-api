using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pruduct.Data.Models.Users;

namespace Pruduct.Data.Configurations.Users;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("AspNetUsers");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.UserName).IsUnique();
        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.HasIndex(x => x.NormalizedUserName).IsUnique();
        builder.Property(x => x.UserName).HasColumnName("Username").IsRequired();
        builder
            .Property(x => x.NormalizedUserName)
            .HasColumnName("NormalizedUsername")
            .IsRequired();
        builder.Property(x => x.NormalizedEmail).IsRequired();
        builder.Property(x => x.NormalizedName).IsRequired();
        builder.Property(x => x.AvatarUrl).HasMaxLength(512);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
    }
}
