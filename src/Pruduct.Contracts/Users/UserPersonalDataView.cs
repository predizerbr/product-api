namespace Pruduct.Contracts.Users;

public class UserPersonalDataView
{
    public string Cpf { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public UserAddressView? Address { get; set; }
}
