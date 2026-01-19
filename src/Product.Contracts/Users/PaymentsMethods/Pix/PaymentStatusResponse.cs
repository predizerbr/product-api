namespace Product.Contracts.Users.PaymentsMethods.Pix;

public class PaymentStatusResponse
{
    public long PaymentId { get; set; }
    public string Status { get; set; } = default!;
    public string? StatusDetail { get; set; }
    public string? ExternalReference { get; set; }
    public decimal? Amount { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsFinal { get; set; }
}
