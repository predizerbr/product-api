using Microsoft.AspNetCore.Mvc;
using Product.Api.Extensions;
using Product.Business.Interfaces.Payments;

namespace Product.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks/mercadopago")]
public class MercadoPagoWebhookController(IMercadoPagoService mercadoPagoService) : ControllerBase
{
    private readonly IMercadoPagoService _mercadoPagoService = mercadoPagoService;

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var result = await _mercadoPagoService.HandleWebhookAsync(Request, ct);
        return this.ToActionResult(result);
    }
}
