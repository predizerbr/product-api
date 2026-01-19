using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Product.Data.Models.Wallet;

namespace Product.Data.Configurations.Wallet;

public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).IsRequired();
        builder.Property(x => x.Amount).IsRequired().HasPrecision(18, 6);
        builder.Property(x => x.ReferenceType).HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);

        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.AccountId, x.CreatedAt });

        builder
            .HasOne(x => x.Account)
            .WithMany(a => a.LedgerEntries)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
