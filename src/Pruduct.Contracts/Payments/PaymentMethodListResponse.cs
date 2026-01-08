using System;
using System.Collections.Generic;

namespace Pruduct.Contracts.Payments;

public class PaymentMethodListResponse
{
    public IReadOnlyCollection<PaymentMethodResponse> Items { get; set; } =
        Array.Empty<PaymentMethodResponse>();
}
