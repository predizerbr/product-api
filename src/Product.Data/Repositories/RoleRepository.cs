using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Users;

namespace Product.Data.Repositories;

public class RoleRepository(AppDbContext db) : IRoleRepository
{
    public async Task<bool> RoleExistsAsync(string roleName, CancellationToken ct = default)
    {
        return await db.Roles.AnyAsync(r => r.Name == roleName, ct);
    }

    public async Task<Role?> GetRoleByNameAsync(string roleName, CancellationToken ct = default)
    {
        return await db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct);
    }

    public async Task AddRoleAsync(Role role, CancellationToken ct = default)
    {
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> UserRoleExistsAsync(
        Guid userId,
        Guid roleId,
        CancellationToken ct = default
    )
    {
        return await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct);
    }

    public async Task AddUserRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = roleId });
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
    }
}
