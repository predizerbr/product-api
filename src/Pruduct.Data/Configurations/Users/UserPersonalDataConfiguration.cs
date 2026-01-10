using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pruduct.Data.Models.Users;

namespace Pruduct.Data.Configurations.Users;

public class UserPersonalDataConfiguration : IEntityTypeConfiguration<UserPersonalData>
{
    public void Configure(EntityTypeBuilder<UserPersonalData> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Cpf).IsUnique();
        builder
            .HasOne(x => x.User)
            .WithOne(x => x.PersonalData)
            .HasForeignKey<UserPersonalData>(x => x.UserId);

        builder
            .HasOne(x => x.Address)
            .WithOne(x => x.PersonalData)
            .HasForeignKey<UserAddress>(x => x.PersonalDataId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
