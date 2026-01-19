using Product.Common.Entities;

namespace Product.Data.Models.Users.PaymentsMethods;

public class UserBankAccount : Entity<Guid>
{
    public Guid UserId { get; set; }

    public string? BankCode { get; set; }
    public string? BankName { get; set; }
    public string? Agency { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountDigit { get; set; }
    public string? AccountType { get; set; }

    public bool IsDefault { get; set; }

    public User? User { get; set; }
}
