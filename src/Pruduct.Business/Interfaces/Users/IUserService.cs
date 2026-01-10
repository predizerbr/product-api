using Pruduct.Business.Interfaces.Results;
using Pruduct.Contracts.Users;

namespace Pruduct.Business.Interfaces.Users;

public interface IUserService
{
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
