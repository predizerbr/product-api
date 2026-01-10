using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pruduct.Api.Contracts;
using Pruduct.Business.Interfaces.Users;
using Pruduct.Contracts.Users;

namespace Pruduct.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _userService.GetMeAsync(userId, ct);
        if (!result.Success)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: result.Error);
        }

        return Ok(new ResponseEnvelope<UserView>(result.Data!));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken ct
    )
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _userService.UpdateProfileAsync(userId, request, ct);
        if (!result.Success)
        {
            var status = result.Error switch
            {
                "email_taken" => StatusCodes.Status400BadRequest,
                "username_taken" => StatusCodes.Status400BadRequest,
                "cpf_taken" => StatusCodes.Status400BadRequest,
                "personal_data_required" => StatusCodes.Status400BadRequest,
                "address_required" => StatusCodes.Status400BadRequest,
                "user_not_found" => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Problem(statusCode: status, title: result.Error);
        }

        return Ok(new ResponseEnvelope<UserView>(result.Data!));
    }

    [HttpPut("me/address")]
    public async Task<IActionResult> UpdateAddress(
        [FromBody] UpdateAddressRequest request,
        CancellationToken ct
    )
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _userService.UpdateAddressAsync(userId, request, ct);
        if (!result.Success)
        {
            var status = result.Error switch
            {
                "personal_data_required" => StatusCodes.Status400BadRequest,
                "address_required" => StatusCodes.Status400BadRequest,
                "user_not_found" => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Problem(statusCode: status, title: result.Error);
        }

        return Ok(new ResponseEnvelope<UserView>(result.Data!));
    }

    [HttpPut("me/avatar")]
    public async Task<IActionResult> UpdateAvatar(
        [FromBody] UpdateAvatarRequest request,
        CancellationToken ct
    )
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _userService.UpdateAvatarAsync(userId, request, ct);
        if (!result.Success)
        {
            var status =
                result.Error == "user_not_found"
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status400BadRequest;
            return Problem(statusCode: status, title: result.Error);
        }

        return Ok(new ResponseEnvelope<UserView>(result.Data!));
    }

    [HttpGet("me/sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _userService.GetSessionsAsync(userId, ct);
        if (!result.Success)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);
        }

        return Ok(new ResponseEnvelope<IReadOnlyCollection<UserSessionResponse>>(result.Data!));
    }

    [HttpDelete("me/sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _userService.RevokeSessionAsync(userId, sessionId, ct);
        if (!result.Success)
        {
            var status =
                result.Error == "session_not_found"
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status400BadRequest;
            return Problem(statusCode: status, title: result.Error);
        }

        return Ok(new ResponseEnvelope<bool>(true));
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }
}
