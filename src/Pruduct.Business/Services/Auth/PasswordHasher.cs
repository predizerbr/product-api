using Microsoft.AspNetCore.Identity;
using Pruduct.Business.Interfaces.Auth;
using Pruduct.Data.Models.Users;

namespace Pruduct.Business.Services.Auth;

public class PasswordHasher : IPasswordHasher, IPasswordHasher<User>
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool Verify(string hash, string password) => BCrypt.Net.BCrypt.Verify(password, hash);

    string IPasswordHasher<User>.HashPassword(User user, string password) => Hash(password);

    PasswordVerificationResult IPasswordHasher<User>.VerifyHashedPassword(
        User user,
        string hashedPassword,
        string providedPassword
    ) =>
        Verify(hashedPassword, providedPassword)
            ? PasswordVerificationResult.Success
            : PasswordVerificationResult.Failed;
}
