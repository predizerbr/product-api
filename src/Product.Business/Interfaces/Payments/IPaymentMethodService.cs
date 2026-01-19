using System.Security.Claims;
using Product.Business.Interfaces.Results;
using Product.Contracts.Users.PaymentsMethods;

namespace Product.Business.Interfaces.Payments;

public interface IPaymentMethodService
{
    Task<ApiResult> GetMethodsApiAsync(ClaimsPrincipal principal, CancellationToken ct = default);

    Task<ApiResult> CreateMethodApiAsync(
        ClaimsPrincipal principal,
        CreatePaymentMethodRequest request,
        CancellationToken ct = default
    );

    Task<ApiResult> DeleteMethodApiAsync(
        ClaimsPrincipal principal,
        Guid methodId,
        CancellationToken ct = default
    );

    Task<ServiceResult<PaymentMethodListResponse>> GetMethodsAsync(
        Guid userId,
        CancellationToken ct = default
    );

    Task<ServiceResult<PaymentMethodResponse>> CreateMethodAsync(
        Guid userId,
        CreatePaymentMethodRequest request,
        CancellationToken ct = default
    );

    Task<ServiceResult<bool>> DeleteMethodAsync(
        Guid userId,
        Guid methodId,
        CancellationToken ct = default
    );
}
