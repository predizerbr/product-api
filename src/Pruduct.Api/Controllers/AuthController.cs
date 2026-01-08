using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pruduct.Api.Contracts;
using Pruduct.Business.Abstractions;
using Pruduct.Contracts.Auth;

namespace Pruduct.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _environment;

    public AuthController(IAuthService authService, IWebHostEnvironment environment)
    {
        _authService = authService;
        _environment = environment;
    }

    [HttpPost("signup")]
    [AllowAnonymous]
    public async Task<IActionResult> Signup([FromBody] SignupRequest request, CancellationToken ct)
    {
        var result = await _authService.SignupAsync(request, ct);
        if (!result.Success)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);
        }

        return Ok(new ResponseEnvelope<AuthResponse>(result.Data!));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        if (!result.Success)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: result.Error);
        }

        return Ok(new ResponseEnvelope<AuthResponse>(result.Data!));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken ct
    )
    {
        var result = await _authService.RefreshAsync(request, ct);
        if (!result.Success)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: result.Error);
        }

        return Ok(new ResponseEnvelope<AuthResponse>(result.Data!));
    }

    [HttpPost("login/google")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginWithGoogle(
        [FromBody] GoogleLoginRequest request,
        CancellationToken ct
    )
    {
        var result = await _authService.LoginWithGoogleAsync(request, ct);
        if (!result.Success)
        {
            var status = result.Error switch
            {
                "google_not_configured" => StatusCodes.Status501NotImplemented,
                "user_inactive" => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status401Unauthorized,
            };
            return Problem(statusCode: status, title: result.Error);
        }

        return Ok(new ResponseEnvelope<AuthResponse>(result.Data!));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _authService.LogoutAsync(userId, request, ct);
        if (!result.Success)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);
        }

        return Ok(new ResponseEnvelope<bool>(true));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken ct
    )
    {
        var result = await _authService.ForgotPasswordAsync(request, ct);
        if (!result.Success)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);
        }

        var response = result.Data!;
        if (!_environment.IsDevelopment())
        {
            response = new ForgotPasswordResponse { ResetToken = null, ExpiresAt = null };
        }

        return Ok(new ResponseEnvelope<ForgotPasswordResponse>(response));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken ct
    )
    {
        var result = await _authService.ResetPasswordAsync(request, ct);
        if (!result.Success)
        {
            var status = result.Error switch
            {
                "invalid_reset_token" => StatusCodes.Status400BadRequest,
                "user_not_found" => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Problem(statusCode: status, title: result.Error);
        }

        return Ok(new ResponseEnvelope<bool>(true));
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailRequest request,
        CancellationToken ct
    )
    {
        var result = await _authService.VerifyEmailAsync(request, ct);
        if (!result.Success)
        {
            var status = result.Error switch
            {
                "invalid_verify_token" => StatusCodes.Status400BadRequest,
                "user_not_found" => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Problem(statusCode: status, title: result.Error);
        }

        return Ok(new ResponseEnvelope<bool>(true));
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub =
            User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }
}
