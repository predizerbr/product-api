using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Product.Data.Models.Webhooks;

namespace Product.Data.Configurations.Users.PaymentsMethods;

public class WebhookEventConfiguration : IEntityTypeConfiguration<MPWebhookEvent>
{
    public void Configure(EntityTypeBuilder<MPWebhookEvent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Provider).IsRequired().HasMaxLength(100);
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Payload).IsRequired();
        builder.Property(e => e.Headers).HasMaxLength(2000);

        builder.HasIndex(e => e.ProviderPaymentId);
    }
}
