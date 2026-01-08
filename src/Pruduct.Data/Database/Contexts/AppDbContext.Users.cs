using Microsoft.EntityFrameworkCore;
using Pruduct.Data.Models;

namespace Pruduct.Data.Database.Contexts;

public partial class AppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserPersonalData> UserPersonalData => Set<UserPersonalData>();
    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
}
