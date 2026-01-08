using Pruduct.Common.Entities;
using Pruduct.Common.Enums;

namespace Pruduct.Data.Models;

public class PaymentIntent : Entity<Guid>
{
    public Guid UserId { get; set; }
    public string Provider { get; set; } = "MANUAL";
    public long Amount { get; set; }
    public string Currency { get; set; } = "BRL";
    public PaymentIntentStatus Status { get; set; } = PaymentIntentStatus.PENDING;
    public string? ExternalPaymentId { get; set; }
    public string IdempotencyKey { get; set; } = default!;
    public DateTimeOffset? ExpiresAt { get; set; }
    public User? User { get; set; }
}
