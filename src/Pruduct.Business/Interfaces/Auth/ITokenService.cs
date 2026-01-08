namespace Pruduct.Business.Abstractions;

public interface ITokenService
{
    string GenerateAccessToken(TokenSubject subject, IReadOnlyCollection<string> roles);
    string GenerateRefreshToken();
    string HashRefreshToken(string rawToken);
}
