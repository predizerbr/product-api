namespace Product.Contracts.Users.PaymentsMethods.Pix;

public class PixResponse
{
    public long PaymentId { get; set; }
    public string? QrCodeBase64 { get; set; }
    public string? QrCode { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string Status { get; set; } = default!;
}
