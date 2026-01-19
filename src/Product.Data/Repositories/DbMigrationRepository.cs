using Microsoft.EntityFrameworkCore;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;

namespace Product.Data.Repositories;

public class DbMigrationRepository(AppDbContext db) : IDbMigrationRepository
{

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
    }
}
