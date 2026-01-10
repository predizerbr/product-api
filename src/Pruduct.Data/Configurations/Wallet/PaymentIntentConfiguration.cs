using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pruduct.Data.Models.Wallet;

namespace Pruduct.Data.Configurations.Wallet;

public class PaymentIntentConfiguration : IEntityTypeConfiguration<PaymentIntent>
{
    public void Configure(EntityTypeBuilder<PaymentIntent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(8);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ExternalPaymentId).HasMaxLength(128);

        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => x.ExternalPaymentId);
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });

        builder
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
