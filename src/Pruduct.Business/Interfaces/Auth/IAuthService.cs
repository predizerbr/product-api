using Pruduct.Business.Abstractions.Results;
using Pruduct.Contracts.Auth;

namespace Pruduct.Business.Abstractions;

public interface IAuthService
{
    Task<ServiceResult<AuthResponse>> SignupAsync(
        SignupRequest request,
        CancellationToken ct = default
    );
    Task<ServiceResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken ct = default
    );
    Task<ServiceResult<AuthResponse>> RefreshAsync(
        RefreshRequest request,
        CancellationToken ct = default
    );
    Task<ServiceResult<AuthResponse>> LoginWithGoogleAsync(
        GoogleLoginRequest request,
        CancellationToken ct = default
    );
    Task<ServiceResult<bool>> LogoutAsync(
        Guid userId,
        LogoutRequest request,
        CancellationToken ct = default
    );
    Task<ServiceResult<ForgotPasswordResponse>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken ct = default
    );
    Task<ServiceResult<bool>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken ct = default
    );
    Task<ServiceResult<bool>> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken ct = default
    );
}
