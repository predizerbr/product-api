using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Product.Api.Extensions;
using Product.Business.Interfaces.Payments;
using Product.Contracts.Users.PaymentsMethods;
using Product.Contracts.Users.PaymentsMethods.Card;

namespace Product.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentMethodService _paymentMethodService;
    private readonly IMercadoPagoService _mercadoPagoService;

    public PaymentsController(
        IPaymentMethodService paymentMethodService,
        IMercadoPagoService mercadoPagoService
    )
    {
        _paymentMethodService = paymentMethodService;
        _mercadoPagoService = mercadoPagoService;
    }

    [HttpGet("methods")]
    public async Task<IActionResult> GetMethods(CancellationToken ct)
    {
        var result = await _paymentMethodService.GetMethodsApiAsync(User, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("methods")]
    public async Task<IActionResult> CreateMethod(
        [FromBody] CreatePaymentMethodRequest request,
        CancellationToken ct
    )
    {
        if (IsCardTokenRequest(request))
        {
            var deviceId = Request.Headers["X-meli-session-id"].ToString();
            if (string.IsNullOrWhiteSpace(deviceId))
                deviceId = request.DeviceId ?? string.Empty;
            var saveReq = new SaveCardRequest
            {
                Payer = request.Payer!,
                Token = request.Token!,
                PaymentMethodId = request.PaymentMethodId,
                IssuerId = request.IssuerId,
                DeviceId = request.DeviceId,
                IsDefault = request.IsDefault,
            };

            var mpResult = await _mercadoPagoService.SaveCardAsync(saveReq, deviceId, ct);
            if (mpResult.StatusCode >= StatusCodes.Status400BadRequest)
            {
                return this.ToActionResult(mpResult);
            }

            if (mpResult.Data is not SaveCardResponse saved)
            {
                return Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "invalid_mp_response"
                );
            }

            var cardHolderName = saved.CardHolderName ?? request.Payer?.CardholderName;
            if (string.IsNullOrWhiteSpace(cardHolderName))
            {
                cardHolderName = "TITULAR";
            }

            var paymentMethodId =
                saved.MpPaymentMethodId
                ?? saved.CardBrand
                ?? request.PaymentMethodId
                ?? string.Empty;

            var createRequest = new CreatePaymentMethodRequest
            {
                Type = "CARD",
                IsDefault = request.IsDefault,
                CardBrand = saved.CardBrand ?? paymentMethodId,
                CardLast4 = saved.CardLast4,
                CardExpMonth = saved.CardExpMonth,
                CardExpYear = saved.CardExpYear,
                CardHolderName = cardHolderName,
                MpCustomerId = saved.MpCustomerId,
                MpCardId = saved.MpCardId,
                MpPaymentMethodId = paymentMethodId,
                HolderIdentification = request.HolderIdentification,
            };

            var createResult = await _paymentMethodService.CreateMethodApiAsync(
                User,
                createRequest,
                ct
            );
            return this.ToActionResult(createResult);
        }

        var result = await _paymentMethodService.CreateMethodApiAsync(User, request, ct);
        return this.ToActionResult(result);
    }

    private static bool IsCardTokenRequest(CreatePaymentMethodRequest request)
    {
        return string.Equals(request.Type, "CARD", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(request.Token)
            && request.Payer is not null
            && !string.IsNullOrWhiteSpace(request.Payer.Email);
    }

    [HttpDelete("methods/{methodId:guid}")]
    public async Task<IActionResult> DeleteMethod(Guid methodId, CancellationToken ct)
    {
        var result = await _paymentMethodService.DeleteMethodApiAsync(User, methodId, ct);
        return this.ToActionResult(result);
    }
}
