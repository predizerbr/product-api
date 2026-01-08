namespace Pruduct.Contracts.Users;

public class UpdateProfileRequest
{
    public string? Name { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? ConfirmPassword { get; set; }
    public string? Cpf { get; set; }
    public string? PhoneNumber { get; set; }
}
