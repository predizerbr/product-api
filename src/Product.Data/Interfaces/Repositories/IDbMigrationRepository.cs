namespace Product.Data.Interfaces.Repositories;

public interface IDbMigrationRepository
{
    Task MigrateAsync(CancellationToken ct = default);
}
