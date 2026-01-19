using Product.Common.Entities;

namespace Product.Data.Models.Users.PaymentsMethods;

public class UserPixKey : Entity<Guid>
{
    public Guid UserId { get; set; }
    public string? PixKey { get; set; }

    public bool IsDefault { get; set; }

    public User? User { get; set; }
}
