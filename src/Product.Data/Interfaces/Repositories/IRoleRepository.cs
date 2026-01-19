using Product.Data.Models.Users;

namespace Product.Data.Interfaces.Repositories;

public interface IRoleRepository
{
    Task<bool> RoleExistsAsync(string roleName, CancellationToken ct = default);
    Task<Role?> GetRoleByNameAsync(string roleName, CancellationToken ct = default);
    Task AddRoleAsync(Role role, CancellationToken ct = default);
    Task<bool> UserRoleExistsAsync(Guid userId, Guid roleId, CancellationToken ct = default);
    Task AddUserRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
