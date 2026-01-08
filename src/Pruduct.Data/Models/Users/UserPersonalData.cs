using Pruduct.Common.Entities;

namespace Pruduct.Data.Models;

public class UserPersonalData : Entity<Guid>
{
    public Guid UserId { get; set; }
    public string Cpf { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public User? User { get; set; }
    public UserAddress? Address { get; set; }
}
