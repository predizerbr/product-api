namespace Product.Contracts.Users.PaymentsMethods;

public partial class CreatePaymentMethodRequest
{
    public string Type { get; set; } = default!;
    public bool? IsDefault { get; set; }

    public string? Token { get; set; }
    public string? PaymentMethodId { get; set; }
    public string? IssuerId { get; set; }
    public string? DeviceId { get; set; }
    public Identification? HolderIdentification { get; set; }
}
