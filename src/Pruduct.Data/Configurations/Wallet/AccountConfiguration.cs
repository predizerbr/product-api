using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pruduct.Data.Models.Wallet;

namespace Pruduct.Data.Configurations.Wallet;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(8).HasDefaultValue("BRL");
        builder.HasIndex(x => new { x.UserId, x.Currency }).IsUnique();

        builder
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
