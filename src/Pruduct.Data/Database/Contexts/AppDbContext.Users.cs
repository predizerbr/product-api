using Microsoft.EntityFrameworkCore;
using Pruduct.Data.Models.Auth;
using Pruduct.Data.Models.Users;

namespace Pruduct.Data.Database.Contexts;

public partial class AppDbContext
{
    public DbSet<UserPersonalData> UserPersonalData => Set<UserPersonalData>();
    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
}
