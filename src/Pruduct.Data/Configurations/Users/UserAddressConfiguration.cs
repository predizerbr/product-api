using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pruduct.Data.Models.Users;

namespace Pruduct.Data.Configurations.Users;

public class UserAddressConfiguration : IEntityTypeConfiguration<UserAddress>
{
    public void Configure(EntityTypeBuilder<UserAddress> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ZipCode).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Street).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Neighborhood).HasMaxLength(128);
        builder.Property(x => x.Number).HasMaxLength(32);
        builder.Property(x => x.Complement).HasMaxLength(128);
        builder.Property(x => x.City).IsRequired().HasMaxLength(64);
        builder.Property(x => x.State).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Country).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.PersonalDataId).IsUnique();
    }
}
