using Product.Data.Models.Auth;
using Product.Data.Models.Users;

namespace Product.Data.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetUserWithPersonalDataAsync(Guid userId, CancellationToken ct = default);
    Task<User?> GetUserWithPersonalDataByEmailAsync(
        string normalizedEmail,
        CancellationToken ct = default
    );
    Task<User?> GetUserByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<bool> IsEmailTakenAsync(Guid userId, string normalizedEmail, CancellationToken ct = default);
    Task<bool> IsUsernameTakenAsync(
        Guid userId,
        string normalizedUsername,
        CancellationToken ct = default
    );
    Task<bool> IsCpfTakenAsync(string cpf, Guid userId, CancellationToken ct = default);
    Task<bool> UserNameExistsAsync(string normalizedUsername, CancellationToken ct = default);
    Task EnsurePersonalDataAsync(Guid userId, CancellationToken ct = default);
    Task AddUserAsync(User user, CancellationToken ct = default);
    Task<string[]> GetUserRolesAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyCollection<RefreshToken>> GetRefreshTokensAsync(
        Guid userId,
        CancellationToken ct = default
    );
    Task<RefreshToken?> GetRefreshTokenAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken ct = default
    );
    Task UpdateUserAsync(User user, CancellationToken ct = default);
    Task UpdateRefreshTokenAsync(RefreshToken token, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
