using System.Text.Json;
using Pruduct.Business.Abstractions;
using Pruduct.Data.Database.Contexts;
using Pruduct.Data.Models;

namespace Pruduct.Business.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db)
    {
        _db = db;
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

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            MetaJson = metaJson,
            Ip = ip,
            UserAgent = userAgent,
        });

        await _db.SaveChangesAsync(ct);
    }
}
