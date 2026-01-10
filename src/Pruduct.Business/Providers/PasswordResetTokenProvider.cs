using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Pruduct.Business.Providers;

public sealed class PasswordResetTokenProvider<TUser> : IUserTwoFactorTokenProvider<TUser>
    where TUser : class
{
    public const string ProviderName = "password_reset";
    public const string OptionsName = "PasswordReset";
    private const string TokenName = "password_reset";
    private const int CodeLength = 6;

    private readonly IOptionsMonitor<DataProtectionTokenProviderOptions> _options;

    public PasswordResetTokenProvider(IOptionsMonitor<DataProtectionTokenProviderOptions> options)
    {
        _options = options;
    }

    public async Task<string> GenerateAsync(string purpose, UserManager<TUser> manager, TUser user)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString($"D{CodeLength}");
        var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"{code}|{issuedAt}";

        await manager.SetAuthenticationTokenAsync(user, ProviderName, TokenName, payload);
        return code;
    }

    public async Task<bool> ValidateAsync(
        string purpose,
        string token,
        UserManager<TUser> manager,
        TUser user
    )
    {
        var stored = await manager.GetAuthenticationTokenAsync(user, ProviderName, TokenName);
        if (string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        var parts = stored.Split('|', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!long.TryParse(parts[1], out var issuedAtSeconds))
        {
            return false;
        }

        var lifespan = _options.Get(OptionsName).TokenLifespan;
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtSeconds);
        if (issuedAt.Add(lifespan) < DateTimeOffset.UtcNow)
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(parts[0]);
        var providedBytes = Encoding.UTF8.GetBytes(token);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<TUser> manager, TUser user) =>
        Task.FromResult(false);
}
