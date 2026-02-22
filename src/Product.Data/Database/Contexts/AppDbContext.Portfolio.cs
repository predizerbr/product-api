using Microsoft.EntityFrameworkCore;
using Product.Data.Models.Portfolio;

namespace Product.Data.Database.Contexts;

public partial class AppDbContext
{
    public DbSet<PositionFill> PositionFills => Set<PositionFill>();
    public DbSet<PortfolioSnapshot> PortfolioSnapshots => Set<PortfolioSnapshot>();
}
