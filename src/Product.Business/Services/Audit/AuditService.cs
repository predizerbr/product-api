using System.Text.Json;
using Product.Business.Interfaces.Audit;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Audit;

namespace Product.Business.Services.Audit;

public class AuditService : IAuditService
{
    private readonly IAuditRepository _auditRepository;

    public AuditService(IAuditRepository auditRepository)
    {
        _auditRepository = auditRepository;
    }

    public async Task LogAsync(
        Guid? userId,
        string action,
        string entity,
        Guid? entityId,
        object? meta = null,
        string? ip = null,
        string? userAgent = null,
        CancellationToken ct = default
    )
    {
        var metaJson = meta is null ? null : JsonSerializer.Serialize(meta);

        await _auditRepository.AddAsync(
            new AuditLog
            {
                UserId = userId,
                Action = action,
                Entity = entity,
                EntityId = entityId,
                MetaJson = metaJson,
                Ip = ip,
                UserAgent = userAgent,
            },
            ct
        );
    }
}
