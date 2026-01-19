using Product.Data.Models.Audit;

namespace Product.Data.Interfaces.Repositories;

public interface IAuditRepository
{
    Task AddAsync(AuditLog log, CancellationToken ct = default);
}
