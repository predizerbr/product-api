using Pruduct.Common.Enums;

namespace Pruduct.Data.Models;

public class UserRole
{
    public Guid UserId { get; set; }
    public RoleName RoleName { get; set; }
}
