using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Product.Data.Models.Portfolio;

namespace Product.Data.Configurations.Portfolio;

public class PortfolioSnapshotConfiguration : IEntityTypeConfiguration<PortfolioSnapshot>
{
    public void Configure(EntityTypeBuilder<PortfolioSnapshot> builder)
    {
        builder.ToTable("PortfolioSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AsOf).IsRequired();
        builder.Property(x => x.TotalInvestedActive).HasPrecision(18, 6);
        builder.Property(x => x.TotalInvestedAllTime).HasPrecision(18, 6);
        builder.Property(x => x.RealizedPnlAllTime).HasPrecision(18, 6);
        builder.Property(x => x.PotentialPnlActive).HasPrecision(18, 6);
        builder.Property(x => x.AccuracyRate).HasPrecision(9, 4);
        builder.HasIndex(x => new { x.UserId, x.AsOf });
    }
}
