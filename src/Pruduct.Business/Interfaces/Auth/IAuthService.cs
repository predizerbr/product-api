using System.Security.Claims;
using Pruduct.Contracts.Auth;

namespace Pruduct.Business.Interfaces.Auth;

public interface IAuthService
{
    Task SignUpAsync(SignupRequest request, CancellationToken ct);
    Task SignInAsync(LoginRequest request, bool? useCookies, bool? useSessionCookies);
    Task SignOutAsync();
    Task RefreshAsync(RefreshRequest request);
    Task ConfirmEmailAsync(Guid userId, string code, string? newEmail);
    Task ResendConfirmationEmailAsync(ResendConfirmationEmailRequest request, CancellationToken ct);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
    Task ChangePasswordAsync(
        ClaimsPrincipal principal,
        ChangePasswordRequest request,
        CancellationToken ct
    );
    Task VerifyResetCodeAsync(VerifyResetCodeRequest request);
    Task<InfoResponse> GetInfoAsync(ClaimsPrincipal principal);
    Task<InfoResponse> UpdateInfoAsync(
        ClaimsPrincipal principal,
        InfoRequest request,
        CancellationToken ct
    );
    Task<TwoFactorResponse> GetTwoFactorAsync(ClaimsPrincipal principal);
    Task<TwoFactorResponse> UpdateTwoFactorAsync(
        ClaimsPrincipal principal,
        TwoFactorRequest request
    );
    Task GoogleLoginAsync(
        GoogleLoginRequest request,
        bool? useCookies = null,
        bool? useSessionCookies = null
    );
    Task<bool> HasExternalLoginAsync(ClaimsPrincipal principal, string? provider = null);
    Task<IEnumerable<string>> GetExternalLoginProvidersAsync(ClaimsPrincipal principal);
}
