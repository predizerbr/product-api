using Pruduct.Business.Abstractions;

namespace Pruduct.Business.Services;

public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool Verify(string hash, string password) => BCrypt.Net.BCrypt.Verify(password, hash);
}
