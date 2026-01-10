using Microsoft.EntityFrameworkCore;
using Pruduct.Data.Models.Audit;

namespace Pruduct.Data.Database.Contexts;

public partial class AppDbContext
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
}
