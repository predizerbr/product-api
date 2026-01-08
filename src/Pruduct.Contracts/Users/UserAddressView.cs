namespace Pruduct.Contracts.Users;

public class UserAddressView
{
    public string ZipCode { get; set; } = default!;
    public string Street { get; set; } = default!;
    public string? Neighborhood { get; set; }
    public string? Number { get; set; }
    public string? Complement { get; set; }
    public string City { get; set; } = default!;
    public string State { get; set; } = default!;
    public string Country { get; set; } = default!;
}
