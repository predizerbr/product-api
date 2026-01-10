namespace Pruduct.Contracts.Users;

public class UserPersonalDataView
{
    public string? Cpf { get; set; }
    public string? PhoneNumber { get; set; }
    public UserAddressView? Address { get; set; }
}
