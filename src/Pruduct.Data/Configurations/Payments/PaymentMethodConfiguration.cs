using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pruduct.Data.Models.Payments;

namespace Pruduct.Data.Configurations.Payments;

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).IsRequired();
        builder.Property(x => x.IsDefault).HasDefaultValue(false);

        builder.Property(x => x.PixKey).HasMaxLength(128);

        builder.Property(x => x.CardBrand).HasMaxLength(32);
        builder.Property(x => x.CardLast4).HasMaxLength(4);
        builder.Property(x => x.CardHolderName).HasMaxLength(128);

        builder.Property(x => x.BankCode).HasMaxLength(16);
        builder.Property(x => x.BankName).HasMaxLength(128);
        builder.Property(x => x.Agency).HasMaxLength(32);
        builder.Property(x => x.AccountNumber).HasMaxLength(32);
        builder.Property(x => x.AccountDigit).HasMaxLength(8);
        builder.Property(x => x.AccountType).HasMaxLength(16);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.PixKey);
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });

        builder
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
