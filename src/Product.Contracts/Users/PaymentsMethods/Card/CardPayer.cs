using Product.Contracts.Users.PaymentsMethods;

namespace Product.Contracts.Users.PaymentsMethods.Card;

public sealed class CardPayer
{
    public string Email { get; set; } = default!;
    public string? CardholderName { get; set; }
    public Identification? Identification { get; set; }
}
