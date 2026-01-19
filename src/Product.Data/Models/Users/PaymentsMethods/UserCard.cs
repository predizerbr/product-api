using Product.Common.Entities;

namespace Product.Data.Models.Users.PaymentsMethods;

public class UserCard : Entity<Guid>
{
    public Guid UserId { get; set; }

    public string? CardBrand { get; set; }
    public string? CardLast4 { get; set; }
    public int? CardExpMonth { get; set; }
    public int? CardExpYear { get; set; }
    public string? CardHolderName { get; set; }
    public string? MpCustomerId { get; set; }
    public string? MpCardId { get; set; }
    public string? MpPaymentMethodId { get; set; }
    public string? CardHolderDocumentType { get; set; }
    public string? CardHolderDocumentNumber { get; set; }
    public string? CardHolderDocumentLast4 { get; set; }

    public bool IsDefault { get; set; }

    public User? User { get; set; }
}
