using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Product.Api.Extensions;
using Product.Business.Interfaces.Auth;
using Product.Contracts.Auth;

namespace Product.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("sign-up")]
    [AllowAnonymous]
    public async Task<IActionResult> SignUp(
        [FromBody] SignupRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _authService.SignUpApiAsync(request, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("sign-in")]
    [AllowAnonymous]
    public async Task<IActionResult> SignIn(
        [FromBody] LoginRequest request,
        [FromQuery] bool? useCookies,
        [FromQuery] bool? useSessionCookies
    )
    {
        var result = await _authService.SignInApiAsync(request, useCookies, useSessionCookies);
        return this.ToActionResult(result);
    }

    [HttpPost("sign-out")]
    [Authorize]
    public async Task<IActionResult> SignOutUser()
    {
        var result = await _authService.SignOutApiAsync();
        return this.ToActionResult(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var result = await _authService.RefreshApiAsync(request);
        return this.ToActionResult(result);
    }

    [HttpPost("confirmEmail")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(
        [FromQuery] Guid userId,
        [FromQuery] string code,
        [FromQuery] string? newEmail
    )
    {
        var result = await _authService.ConfirmEmailApiAsync(userId, code, newEmail);
        return this.ToActionResult(result);
    }

    [HttpPost("resendConfirmationEmail")]
    [AllowAnonymous]
    public async Task<IActionResult> Resend(
        [FromBody] ResendConfirmationEmailRequest request,
        CancellationToken ct
    )
    {
        var result = await _authService.ResendConfirmationEmailApiAsync(request, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("resend-reset-code")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendResetCode(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken ct
    )
    {
        var result = await _authService.ResendResetCodeApiAsync(request, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("sign-in/google")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleSignIn(
        [FromBody] GoogleLoginRequest request,
        [FromQuery] bool? useCookies,
        [FromQuery] bool? useSessionCookies
    )
    {
        var result = await _authService.GoogleSignInApiAsync(
            request,
            useCookies,
            useSessionCookies
        );
        return this.ToActionResult(result);
    }

    [HttpPost("forgotPassword")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken ct
    )
    {
        var result = await _authService.ForgotPasswordApiAsync(request, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("resetPassword")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken ct
    )
    {
        var result = await _authService.ResetPasswordApiAsync(request, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("verify-reset-code")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeRequest request)
    {
        var result = await _authService.VerifyResetCodeApiAsync(request);
        return this.ToActionResult(result);
    }

    [HttpGet("manage/info")]
    [Authorize]
    public async Task<IActionResult> GetInfo()
    {
        var result = await _authService.GetInfoApiAsync(User);
        return this.ToActionResult(result);
    }

    [HttpPost("manage/info")]
    [Authorize]
    public async Task<IActionResult> UpdateInfo(
        [FromBody] InfoRequest request,
        CancellationToken ct
    )
    {
        var result = await _authService.UpdateInfoApiAsync(User, request, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("manage/2fa")]
    [Authorize]
    public async Task<IActionResult> GetTwoFactor()
    {
        var result = await _authService.GetTwoFactorApiAsync(User);
        return this.ToActionResult(result);
    }

    [HttpPost("manage/2fa")]
    [Authorize]
    public async Task<IActionResult> UpdateTwoFactor([FromBody] TwoFactorRequest request)
    {
        var result = await _authService.UpdateTwoFactorApiAsync(User, request);
        return this.ToActionResult(result);
    }

    [HttpGet("manage/external-login")]
    [Authorize]
    public async Task<IActionResult> HasExternalLogin()
    {
        var result = await _authService.HasExternalLoginApiAsync(User);
        return this.ToActionResult(result);
    }

    [HttpPost("manage/password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct
    )
    {
        var result = await _authService.ChangePasswordApiAsync(User, request, ct);
        return this.ToActionResult(result);
    }
}
