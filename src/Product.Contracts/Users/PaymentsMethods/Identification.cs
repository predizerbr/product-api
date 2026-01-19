namespace Product.Contracts.Users.PaymentsMethods;

public sealed class Identification
{
    public string Type { get; set; } = default!;
    public string Number { get; set; } = default!;
}
