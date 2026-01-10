using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pruduct.Business.Interfaces.Auth;
using Pruduct.Contracts.Auth;

namespace Pruduct.Api.Controllers;

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
        await _authService.SignUpAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("sign-in")]
    [AllowAnonymous]
    public async Task<IActionResult> SignIn(
        [FromBody] LoginRequest request,
        [FromQuery] bool? useCookies,
        [FromQuery] bool? useSessionCookies
    )
    {
        await _authService.SignInAsync(request, useCookies, useSessionCookies);
        return Ok();
    }

    [HttpPost("sign-out")]
    [Authorize]
    public async Task<IActionResult> SignOutUser()
    {
        await _authService.SignOutAsync();
        return NoContent();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        await _authService.RefreshAsync(request);
        return Ok();
    }

    [HttpPost("confirmEmail")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(
        [FromQuery] Guid userId,
        [FromQuery] string code,
        [FromQuery] string? newEmail
    )
    {
        await _authService.ConfirmEmailAsync(userId, code, newEmail);
        return Ok();
    }

    [HttpPost("resendConfirmationEmail")]
    [AllowAnonymous]
    public async Task<IActionResult> Resend(
        [FromBody] ResendConfirmationEmailRequest request,
        CancellationToken ct
    )
    {
        await _authService.ResendConfirmationEmailAsync(request, ct);
        return Ok();
    }

    [HttpPost("resend-reset-code")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendResetCode(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken ct
    )
    {
        // Reuse forgot-password flow to resend the password reset code.
        await _authService.ForgotPasswordAsync(request, ct);
        return Ok();
    }

    [HttpPost("sign-in/google")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleSignIn(
        [FromBody] GoogleLoginRequest request,
        [FromQuery] bool? useCookies,
        [FromQuery] bool? useSessionCookies
    )
    {
        try
        {
            await _authService.GoogleLoginAsync(request, useCookies, useSessionCookies);
            return Ok();
        }
        catch (UnauthorizedAccessException)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "invalid_google_token"
            );
        }
    }

    [HttpPost("forgotPassword")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken ct
    )
    {
        await _authService.ForgotPasswordAsync(request, ct);
        return Ok();
    }

    [HttpPost("resetPassword")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken ct
    )
    {
        await _authService.ResetPasswordAsync(request, ct);
        return Ok();
    }

    [HttpPost("verify-reset-code")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeRequest request)
    {
        try
        {
            await _authService.VerifyResetCodeAsync(request);
            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { title = "user_not_found" });
        }
        catch (ArgumentException ex) when (ex.Message == "invalid_reset_token")
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "invalid_reset_token"
            );
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ex.Message);
        }
    }

    [HttpGet("manage/info")]
    [Authorize]
    public async Task<IActionResult> GetInfo()
    {
        var info = await _authService.GetInfoAsync(User);
        return Ok(info);
    }

    [HttpPost("manage/info")]
    [Authorize]
    public async Task<IActionResult> UpdateInfo(
        [FromBody] InfoRequest request,
        CancellationToken ct
    )
    {
        try
        {
            var info = await _authService.UpdateInfoAsync(User, request, ct);
            return Ok(info);
        }
        catch (InvalidOperationException ex)
            when (ex.Message == "external_account_cannot_change_password")
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "external_account_cannot_change_password"
            );
        }
    }

    [HttpGet("manage/2fa")]
    [Authorize]
    public async Task<IActionResult> GetTwoFactor()
    {
        var resp = await _authService.GetTwoFactorAsync(User);
        return Ok(resp);
    }

    [HttpPost("manage/2fa")]
    [Authorize]
    public async Task<IActionResult> UpdateTwoFactor([FromBody] TwoFactorRequest request)
    {
        var resp = await _authService.UpdateTwoFactorAsync(User, request);
        return Ok(resp);
    }

    [HttpGet("manage/external-login")]
    [Authorize]
    public async Task<IActionResult> HasExternalLogin()
    {
        var providers = await _authService.GetExternalLoginProvidersAsync(User);
        var has = providers != null && providers.Any();
        return Ok(new { hasExternalLogin = has, providers });
    }

    [HttpPost("manage/password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct
    )
    {
        try
        {
            await _authService.ChangePasswordAsync(User, request, ct);
            return Ok();
        }
        catch (UnauthorizedAccessException)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }
        catch (ArgumentException ex) when (ex.Message == "password_mismatch")
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "password_mismatch");
        }
        catch (InvalidOperationException ex)
            when (ex.Message == "external_account_cannot_change_password")
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "external_account_cannot_change_password"
            );
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ex.Message);
        }
    }
}
