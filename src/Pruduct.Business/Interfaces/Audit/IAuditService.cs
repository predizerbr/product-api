namespace Pruduct.Business.Abstractions;

public interface IAuditService
{
    Task LogAsync(
        Guid? userId,
        string action,
        string entity,
        Guid? entityId,
        object? meta = null,
        string? ip = null,
        string? userAgent = null,
        CancellationToken ct = default
    );
}
