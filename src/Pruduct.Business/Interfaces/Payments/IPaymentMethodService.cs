using Pruduct.Business.Abstractions.Results;
using Pruduct.Contracts.Payments;

namespace Pruduct.Business.Abstractions;

public interface IPaymentMethodService
{
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
