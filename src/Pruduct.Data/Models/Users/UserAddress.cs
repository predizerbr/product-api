using Pruduct.Common.Entities;

namespace Pruduct.Data.Models;

public class UserAddress : Entity<Guid>
{
    public Guid PersonalDataId { get; set; }
    public string ZipCode { get; set; } = default!;
    public string Street { get; set; } = default!;
    public string? Neighborhood { get; set; }
    public string? Number { get; set; }
    public string? Complement { get; set; }
    public string City { get; set; } = default!;
    public string State { get; set; } = default!;
    public string Country { get; set; } = "BR";
    public UserPersonalData? PersonalData { get; set; }
}
