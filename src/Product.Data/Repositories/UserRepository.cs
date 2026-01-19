using Microsoft.EntityFrameworkCore;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Auth;
using Product.Data.Models.Users;

namespace Product.Data.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public async Task<User?> GetUserWithPersonalDataAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return await db
            .Users.Include(u => u.PersonalData)
            .ThenInclude(pd => pd!.Address)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
    }

    public async Task<User?> GetUserWithPersonalDataByEmailAsync(
        string normalizedEmail,
        CancellationToken ct = default
    )
    {
        return await db
            .Users.Include(u => u.PersonalData)
            .ThenInclude(pd => pd!.Address)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
    }

    public async Task<User?> GetUserByEmailAsync(
        string normalizedEmail,
        CancellationToken ct = default
    )
    {
        return await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
    }

    public async Task<bool> IsEmailTakenAsync(
        Guid userId,
        string normalizedEmail,
        CancellationToken ct = default
    )
    {
        return await db.Users.AnyAsync(
            u => u.Id != userId && u.NormalizedEmail == normalizedEmail,
            ct
        );
    }

    public async Task<bool> IsUsernameTakenAsync(
        Guid userId,
        string normalizedUsername,
        CancellationToken ct = default
    )
    {
        return await db.Users.AnyAsync(
            u => u.Id != userId && u.NormalizedUserName == normalizedUsername,
            ct
        );
    }

    public async Task<bool> IsCpfTakenAsync(string cpf, Guid userId, CancellationToken ct = default)
    {
        return await db.UserPersonalData.AnyAsync(x => x.UserId != userId && x.Cpf == cpf, ct);
    }

    public async Task<bool> UserNameExistsAsync(
        string normalizedUsername,
        CancellationToken ct = default
    )
    {
        return await db.Users.AnyAsync(u => u.NormalizedUserName == normalizedUsername, ct);
    }

    public async Task EnsurePersonalDataAsync(Guid userId, CancellationToken ct = default)
    {
        if (await db.UserPersonalData.AnyAsync(x => x.UserId == userId, ct))
        {
            return;
        }

        db.UserPersonalData.Add(new UserPersonalData { UserId = userId });
        await db.SaveChangesAsync(ct);
    }

    public async Task AddUserAsync(User user, CancellationToken ct = default)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task<string[]> GetUserRolesAsync(Guid userId, CancellationToken ct = default)
    {
        return await db
            .UserRoles.Where(ur => ur.UserId == userId)
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name ?? string.Empty)
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyCollection<RefreshToken>> GetRefreshTokensAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return await db
            .RefreshTokens.Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<RefreshToken?> GetRefreshTokenAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken ct = default
    )
    {
        return await db.RefreshTokens.FirstOrDefaultAsync(
            x => x.Id == sessionId && x.UserId == userId,
            ct
        );
    }

    public async Task UpdateUserAsync(User user, CancellationToken ct = default)
    {
        db.Users.Update(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateRefreshTokenAsync(RefreshToken token, CancellationToken ct = default)
    {
        db.RefreshTokens.Update(token);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
    }
}
