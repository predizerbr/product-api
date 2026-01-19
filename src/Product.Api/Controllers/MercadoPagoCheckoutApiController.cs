using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Product.Api.Extensions;
using Product.Business.Interfaces.Payments;
using Product.Contracts.Users.PaymentsMethods;
using Product.Contracts.Users.PaymentsMethods.Card;
using Product.Contracts.Users.PaymentsMethods.Pix;
using Product.Data.Interfaces.Repositories;

namespace Product.Api.Controllers;

[ApiController]
[Route("api/v1/payments/mercadopago")]
public class MercadoPagoCheckoutApiController(
    IMercadoPagoService mercadoPagoService,
    IPaymentMethodRepository paymentMethodRepository,
    Microsoft.Extensions.Logging.ILogger<MercadoPagoCheckoutApiController> logger
) : ControllerBase
{
    private readonly IMercadoPagoService _mercadoPagoService = mercadoPagoService;
    private readonly IPaymentMethodRepository _paymentMethodRepository = paymentMethodRepository;
    private readonly Microsoft.Extensions.Logging.ILogger<MercadoPagoCheckoutApiController> _logger = logger;

    [HttpPost("orders/card")]
    public async Task<IActionResult> CreateCardOrder(
        [FromBody] CreateCardOrderRequest req,
        CancellationToken ct = default
    )
    {
        _logger.LogInformation("CreateCardOrder called: OrderId={OrderId}, Amount={Amount}", req.OrderId, req.Amount);
        var deviceId = Request.Headers["X-meli-session-id"].ToString();
        if (string.IsNullOrWhiteSpace(deviceId))
            deviceId = req.DeviceId ?? string.Empty;

        _logger.LogDebug("DeviceId resolved: {DeviceId}", deviceId);

        req.Payer ??= new CardPayer();

        // ✅ fluxo cartão salvo (sem token)
        var usingSavedCard = string.IsNullOrWhiteSpace(req.Token) && !string.IsNullOrWhiteSpace(req.MpCardId);

        // se veio mpCardId e não veio identification => busca no banco
        if (usingSavedCard && req.Payer.Identification is null)
        {
            if (TryGetUserId(out var userId))
            {
                var card = await _paymentMethodRepository.GetUserCardByMpCardIdAsync(
                    userId,
                    req.MpCardId!,
                    ct
                );

                if (card is not null)
                {
                    _logger.LogInformation("Found saved card for user {UserId} mpCardId={MpCardId}", userId, req.MpCardId);
                    // ✅ completa nome do titular
                    if (!string.IsNullOrWhiteSpace(card.CardHolderName) && string.IsNullOrWhiteSpace(req.Payer.CardholderName))
                        req.Payer.CardholderName = card.CardHolderName;

                    // ✅ completa CPF/CNPJ
                    if (!string.IsNullOrWhiteSpace(card.CardHolderDocumentNumber))
                    {
                        req.Payer.Identification = new Identification
                        {
                            Type = card.CardHolderDocumentType
                                   ?? (card.CardHolderDocumentNumber.Length > 11 ? "CNPJ" : "CPF"),
                            Number = card.CardHolderDocumentNumber,
                        };
                    }
                }
            }
        }

        var digits = OnlyDigits(req.Payer?.Identification?.Number);
        if (digits.Length < 11)
            return BadRequest(new { message = "CPF/CNPJ do titular é obrigatório para pagar." });

        req.Payer!.Identification!.Number = digits;

        if (usingSavedCard)
        {
            var cvv = OnlyDigits(req.SecurityCode);
            if (cvv.Length < 3)
                return BadRequest(new { message = "CVV é obrigatório para pagar com cartão salvo." });

            req.SecurityCode = cvv;
        }

        _logger.LogInformation("Calling MercadoPagoService.CreateCardOrderAsync for OrderId={OrderId}", req.OrderId);
        var result = await _mercadoPagoService.CreateCardOrderAsync(req, deviceId, ct);
        _logger.LogInformation("CreateCardOrder result: Status={Status} Error={Error}", result.StatusCode, result.Error);
        return this.ToActionResult(result);
    }

    [HttpGet("orders/{orderIdOrMpId}/status")]
    public async Task<IActionResult> GetOrderStatus(
        [FromRoute] string orderIdOrMpId,
        CancellationToken ct = default
    )
    {
        var result = await _mercadoPagoService.GetOrderStatusAsync(orderIdOrMpId, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("pix")]
    public async Task<IActionResult> CreatePix(
        [FromBody] CreatePixRequest req,
        CancellationToken ct = default
    )
    {
        var deviceId = Request.Headers["X-meli-session-id"].ToString();
        if (string.IsNullOrWhiteSpace(deviceId))
            deviceId = req.DeviceId ?? string.Empty;
        var result = await _mercadoPagoService.CreatePixAsync(req, deviceId, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("status/{paymentId:long}")]
    public async Task<IActionResult> GetPaymentStatus(
        [FromRoute] long paymentId,
        CancellationToken ct = default
    )
    {
        var result = await _mercadoPagoService.GetPaymentStatusAsync(paymentId, ct);
        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }

    private static string OnlyDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value.Where(char.IsDigit).ToArray());
    }
}
