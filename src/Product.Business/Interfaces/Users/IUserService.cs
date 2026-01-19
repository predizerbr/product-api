using System.Security.Claims;
using Product.Business.Interfaces.Results;
using Product.Contracts.Users;

namespace Product.Business.Interfaces.Users;

public interface IUserService
{
    Task<ApiResult> GetMeApiAsync(ClaimsPrincipal principal, CancellationToken ct = default);

    Task<ApiResult> UpdateProfileApiAsync(
        ClaimsPrincipal principal,
        UpdateProfileRequest request,
        CancellationToken ct = default
    );

    Task<ApiResult> UpdateAddressApiAsync(
        ClaimsPrincipal principal,
        UpdateAddressRequest request,
        CancellationToken ct = default
    );

    Task<ApiResult> UpdateAvatarApiAsync(
        ClaimsPrincipal principal,
        UpdateAvatarRequest request,
        CancellationToken ct = default
    );

    Task<ApiResult> GetSessionsApiAsync(ClaimsPrincipal principal, CancellationToken ct = default);

    Task<ApiResult> RevokeSessionApiAsync(
        ClaimsPrincipal principal,
        Guid sessionId,
        CancellationToken ct = default
    );

    Task<ServiceResult<UserView>> GetMeAsync(Guid userId, CancellationToken ct = default);
    Task<ServiceResult<UserView>> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken ct = default
    );
    Task<ServiceResult<UserView>> UpdateAddressAsync(
        Guid userId,
        UpdateAddressRequest request,
        CancellationToken ct = default
    );
    Task<ServiceResult<UserView>> UpdateAvatarAsync(
        Guid userId,
        UpdateAvatarRequest request,
        CancellationToken ct = default
    );
    Task<ServiceResult<IReadOnlyCollection<UserSessionResponse>>> GetSessionsAsync(
        Guid userId,
        CancellationToken ct = default
    );
    Task<ServiceResult<bool>> RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken ct = default
    );
}
