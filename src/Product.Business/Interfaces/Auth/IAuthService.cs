using System.Security.Claims;
using Product.Business.Interfaces.Results;
using Product.Contracts.Auth;

namespace Product.Business.Interfaces.Auth;

public interface IAuthService
{
    Task<ApiResult> SignUpApiAsync(SignupRequest request, CancellationToken ct);
    Task<ApiResult> SignInApiAsync(
        LoginRequest request,
        bool? useCookies,
        bool? useSessionCookies
    );
    Task<ApiResult> SignOutApiAsync();
    Task<ApiResult> RefreshApiAsync(RefreshRequest request);
    Task<ApiResult> ConfirmEmailApiAsync(Guid userId, string code, string? newEmail);
    Task<ApiResult> ResendConfirmationEmailApiAsync(
        ResendConfirmationEmailRequest request,
        CancellationToken ct
    );
    Task<ApiResult> ResendResetCodeApiAsync(ForgotPasswordRequest request, CancellationToken ct);
    Task<ApiResult> GoogleSignInApiAsync(
        GoogleLoginRequest request,
        bool? useCookies = null,
        bool? useSessionCookies = null
    );
    Task<ApiResult> ForgotPasswordApiAsync(ForgotPasswordRequest request, CancellationToken ct);
    Task<ApiResult> ResetPasswordApiAsync(ResetPasswordRequest request, CancellationToken ct);
    Task<ApiResult> VerifyResetCodeApiAsync(VerifyResetCodeRequest request);
    Task<ApiResult> GetInfoApiAsync(ClaimsPrincipal principal);
    Task<ApiResult> UpdateInfoApiAsync(
        ClaimsPrincipal principal,
        InfoRequest request,
        CancellationToken ct
    );
    Task<ApiResult> GetTwoFactorApiAsync(ClaimsPrincipal principal);
    Task<ApiResult> UpdateTwoFactorApiAsync(
        ClaimsPrincipal principal,
        TwoFactorRequest request
    );
    Task<ApiResult> HasExternalLoginApiAsync(ClaimsPrincipal principal);
    Task<ApiResult> ChangePasswordApiAsync(
        ClaimsPrincipal principal,
        ChangePasswordRequest request,
        CancellationToken ct
    );

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
