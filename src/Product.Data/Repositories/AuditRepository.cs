using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Audit;

namespace Product.Data.Repositories;

public class AuditRepository(AppDbContext db) : IAuditRepository
{
    public async Task AddAsync(AuditLog log, CancellationToken ct = default)
    {
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync(ct);
    }
}
