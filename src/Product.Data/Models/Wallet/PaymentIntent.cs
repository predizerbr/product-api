using Product.Common.Entities;
using Product.Common.Enums;
using Product.Data.Models.Users;

namespace Product.Data.Models.Wallet;

public class PaymentIntent : Entity<Guid>
{
    public Guid UserId { get; set; }
    public string Provider { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BRL";
    public PaymentIntentStatus Status { get; set; } = PaymentIntentStatus.PENDING;
    public string? ExternalPaymentId { get; set; }
    public string IdempotencyKey { get; set; } = default!;
    public DateTimeOffset? ExpiresAt { get; set; }
    public User? User { get; set; }
}
