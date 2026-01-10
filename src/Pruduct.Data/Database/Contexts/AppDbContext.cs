using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pruduct.Data.Models.Users;

namespace Pruduct.Data.Database.Contexts;

public partial class AppDbContext : IdentityDbContext<User, Role, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }
}
