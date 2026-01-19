using Product.Common.Entities;

namespace Product.Data.Models.Orders;

public class Order : Entity<Guid>
{
    public string OrderId { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BRL";
    public string Provider { get; set; } = string.Empty;
    public long? ProviderPaymentId { get; set; }
    public string? ProviderPaymentIdText { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public bool Credited { get; set; }
    public string Status { get; set; } = "created"; // created | pending | approved | rejected
    public string? StatusDetail { get; set; }
    public string PaymentMethod { get; set; } = string.Empty; // pix
}
