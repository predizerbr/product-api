using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Product.Data.Models.Orders;

namespace Product.Data.Configurations.Users.PaymentsMethods;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Amount).IsRequired().HasPrecision(18, 6);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(8);
        builder.Property(x => x.Provider).IsRequired().HasMaxLength(64);
        builder.Property(x => x.PaymentMethod).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ProviderPaymentIdText).HasMaxLength(64);

        builder.HasIndex(x => x.ProviderPaymentId).IsUnique(false);
        builder.HasIndex(x => x.ProviderPaymentIdText).IsUnique(false);
        builder.HasIndex(x => x.OrderId).IsUnique(false);
    }
}
