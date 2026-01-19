using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Product.Api.Extensions;
using Product.Business.Interfaces.Users;
using Product.Contracts.Users;

namespace Product.Api.Controllers;

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
        var result = await _userService.GetMeApiAsync(User, ct);
        return this.ToActionResult(result);
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken ct
    )
    {
        var result = await _userService.UpdateProfileApiAsync(User, request, ct);
        return this.ToActionResult(result);
    }

    [HttpPut("me/address")]
    public async Task<IActionResult> UpdateAddress(
        [FromBody] UpdateAddressRequest request,
        CancellationToken ct
    )
    {
        var result = await _userService.UpdateAddressApiAsync(User, request, ct);
        return this.ToActionResult(result);
    }

    [HttpPut("me/avatar")]
    public async Task<IActionResult> UpdateAvatar(
        [FromBody] UpdateAvatarRequest request,
        CancellationToken ct
    )
    {
        var result = await _userService.UpdateAvatarApiAsync(User, request, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("me/sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken ct)
    {
        var result = await _userService.GetSessionsApiAsync(User, ct);
        return this.ToActionResult(result);
    }

    [HttpDelete("me/sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken ct)
    {
        var result = await _userService.RevokeSessionApiAsync(User, sessionId, ct);
        return this.ToActionResult(result);
    }
}
