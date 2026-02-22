using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Product.Data.Models.Portfolio;

namespace Product.Data.Configurations.Portfolio;

public class PositionFillConfiguration : IEntityTypeConfiguration<PositionFill>
{
    public void Configure(EntityTypeBuilder<PositionFill> builder)
    {
        builder.ToTable("PositionFills");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Side).IsRequired().HasMaxLength(8);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Price).HasPrecision(18, 6);
        builder.Property(x => x.GrossAmount).HasPrecision(18, 6);
        builder.Property(x => x.FeeAmount).HasPrecision(18, 6);
        builder.Property(x => x.NetAmount).HasPrecision(18, 6);
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.HasIndex(x => new { x.MarketId, x.CreatedAt });
        builder.HasIndex(x => x.PositionId);
        builder.HasIndex(x => x.IdempotencyKey);
    }
}
