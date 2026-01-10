using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pruduct.Data.Models.Emails;

namespace Pruduct.Data.Configurations.Emails;

public class QueuedEmailConfiguration : IEntityTypeConfiguration<QueuedEmail>
{
    public void Configure(EntityTypeBuilder<QueuedEmail> builder)
    {
        builder.ToTable("QueuedEmails");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ToEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ToName).HasMaxLength(128);
        builder.Property(x => x.Subject).HasMaxLength(256).IsRequired();
        builder.Property(x => x.HtmlBody).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(2048);
        builder.HasIndex(x => x.SentAt);
        builder.HasIndex(x => new { x.SentAt, x.CreatedAt });
    }
}
