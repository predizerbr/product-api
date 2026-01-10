using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pruduct.Api.Contracts;
using Pruduct.Business.Interfaces.Payments;
using Pruduct.Contracts.Payments;

namespace Pruduct.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentMethodService _paymentMethodService;

    public PaymentsController(IPaymentMethodService paymentMethodService)
    {
        _paymentMethodService = paymentMethodService;
    }

    [HttpGet("methods")]
    public async Task<IActionResult> GetMethods(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _paymentMethodService.GetMethodsAsync(userId, ct);
        if (!result.Success)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);
        }

        return Ok(new ResponseEnvelope<PaymentMethodListResponse>(result.Data!));
    }

    [HttpPost("methods")]
    public async Task<IActionResult> CreateMethod(
        [FromBody] CreatePaymentMethodRequest request,
        CancellationToken ct
    )
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _paymentMethodService.CreateMethodAsync(userId, request, ct);
        if (!result.Success)
        {
            var status = result.Error switch
            {
                "invalid_payment_type" => StatusCodes.Status400BadRequest,
                "pix_key_required" => StatusCodes.Status400BadRequest,
                "card_required" => StatusCodes.Status400BadRequest,
                "bank_account_required" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest,
            };
            return Problem(statusCode: status, title: result.Error);
        }

        return Ok(new ResponseEnvelope<PaymentMethodResponse>(result.Data!));
    }

    [HttpDelete("methods/{methodId:guid}")]
    public async Task<IActionResult> DeleteMethod(Guid methodId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _paymentMethodService.DeleteMethodAsync(userId, methodId, ct);
        if (!result.Success)
        {
            var status =
                result.Error == "payment_method_not_found"
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
