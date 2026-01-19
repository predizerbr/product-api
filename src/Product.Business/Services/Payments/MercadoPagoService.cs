using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Product.Business.Interfaces.Payments;
using Product.Business.Interfaces.Results;
using Product.Business.Interfaces.Wallet;
using Product.Business.Options;
using Product.Contracts.Users.PaymentsMethods.Card;
using Product.Contracts.Users.PaymentsMethods.Pix;
using Product.Data.Models.Webhooks;

namespace Product.Business.Services.Payments;

public class MercadoPagoService : IMercadoPagoService
{
    private const int PixExpirationMinutes = 15;
    private const string WalletDepositPrefix = "WALLET_DEPOSIT_";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MercadoPagoOptions _options;
    private readonly IOrderService _orderService;
    private readonly IWebhookService _webhookService;
    private readonly IWalletService _walletService;
    private readonly ILogger<MercadoPagoService> _logger;

    public MercadoPagoService(
        IHttpClientFactory httpClientFactory,
        IOptions<MercadoPagoOptions> options,
        IOrderService orderService,
        IWebhookService webhookService,
        IWalletService walletService,
        ILogger<MercadoPagoService> logger
    )
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _orderService = orderService;
        _webhookService = webhookService;
        _walletService = walletService;
        _logger = logger;
    }

    public async Task<ApiResult> CreateCardOrderAsync(
        CreateCardOrderRequest req,
        string? deviceId,
        CancellationToken ct = default
    )
    {
        if (
            req.Amount <= 0
            || string.IsNullOrWhiteSpace(req.OrderId)
            || string.IsNullOrWhiteSpace(req.Token)
            || string.IsNullOrWhiteSpace(req.PaymentMethodId)
            || req.Payer is null
            || string.IsNullOrWhiteSpace(req.Payer.Email)
        )
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, "invalid_request");
        }

        var client = CreateMpClient(GetAccessToken());

        client.DefaultRequestHeaders.Remove("X-Idempotency-Key");
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", req.OrderId);

        var amount = FormatAmount(req.Amount);
        var installments = req.Installments <= 0 ? 1 : req.Installments;
        var paymentType = string.IsNullOrWhiteSpace(req.PaymentType)
            ? "credit_card"
            : req.PaymentType.Trim().ToLowerInvariant();

        var normalizedPaymentMethodId = NormalizePaymentMethodId(
            req.PaymentMethodId,
            paymentType
        );
        var paymentMethod = new Dictionary<string, object?>
        {
            ["id"] = normalizedPaymentMethodId,
            ["type"] = paymentType,
            ["token"] = req.Token,
            ["installments"] = installments,
        };

        if (!string.IsNullOrWhiteSpace(req.IssuerId))
        {
            paymentMethod["issuer_id"] = req.IssuerId;
        }

        var payer = new Dictionary<string, object?> { ["email"] = req.Payer.Email };

        if (
            req.Payer.Identification is not null
            && !string.IsNullOrWhiteSpace(req.Payer.Identification.Type)
            && !string.IsNullOrWhiteSpace(req.Payer.Identification.Number)
        )
        {
            payer["identification"] = new
            {
                type = req.Payer.Identification.Type,
                number = req.Payer.Identification.Number,
            };
        }

        AddPayerName(payer, req.Payer.CardholderName);

        var payload = new Dictionary<string, object?>
        {
            ["type"] = "online",
            ["processing_mode"] = "automatic",
            ["total_amount"] = amount,
            ["external_reference"] = req.OrderId,
            ["payer"] = payer,
            ["transactions"] = new Dictionary<string, object?>
            {
                ["payments"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["amount"] = amount,
                        ["payment_method"] = paymentMethod,
                    },
                },
            },
        };

        var httpReq = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.mercadopago.com/v1/orders"
        )
        {
            Content = JsonContent.Create(payload),
        };

        var finalDeviceId = !string.IsNullOrWhiteSpace(deviceId) ? deviceId : req.DeviceId;
        if (!string.IsNullOrWhiteSpace(finalDeviceId))
        {
            httpReq.Headers.TryAddWithoutValidation("X-meli-session-id", finalDeviceId);
        }

        var resp = await client.SendAsync(httpReq, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            return BuildMpErrorResponse(resp, body);
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var providerOrderId = TryGetString(root, "id");
        var externalReference = TryGetString(root, "external_reference") ?? req.OrderId;

        var orderStatus = TryGetString(root, "status");
        var orderStatusDetail = TryGetString(root, "status_detail");

        string? paymentStatus = null;
        string? paymentStatusDetail = null;
        string? paymentId = null;
        decimal? paymentAmount = null;

        if (TryGetFirstPayment(root, out var payment))
        {
            paymentStatus = TryGetString(payment, "status");
            paymentStatusDetail = TryGetString(payment, "status_detail");
            paymentId = TryGetString(payment, "id");
            if (TryGetDecimal(payment, "amount", out var amountValue))
            {
                paymentAmount = amountValue;
            }
        }

        if (!paymentAmount.HasValue && TryGetDecimal(root, "total_amount", out var totalAmount))
        {
            paymentAmount = totalAmount;
        }

        var status = paymentStatus ?? orderStatus;
        var statusDetail = paymentStatusDetail ?? orderStatusDetail;
        var normalized = NormalizeMpStatus(status);

        if (string.Equals(statusDetail, "expired", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "expired";
        }

        var isFinal = IsFinalStatus(normalized);
        var finalAmount = paymentAmount ?? req.Amount;

        try
        {
            await _orderService.CreateOrUpdateAsync(
                externalReference,
                finalAmount,
                "BRL",
                "mercadopago",
                TryParseLong(paymentId),
                paymentId,
                normalized ?? status ?? "pending",
                statusDetail,
                "card",
                null,
                ct
            );
        }
        catch { }

        if (TryParsePaymentIntentId(externalReference, out var intentId))
        {
            try
            {
                await _walletService.SyncDepositStatusAsync(
                    intentId,
                    status,
                    statusDetail,
                    paymentId ?? providerOrderId,
                    paymentAmount,
                    ct
                );
            }
            catch { }
        }

        return ApiResult.Ok(
            new CardOrderResponse
            {
                OrderId = externalReference,
                ProviderOrderId = providerOrderId,
                ProviderPaymentId = paymentId,
                Status = normalized ?? "unknown",
                StatusDetail = statusDetail,
                IsFinal = isFinal,
                Amount = finalAmount,
            }
        );
    }

    public async Task<ApiResult> GetOrderStatusAsync(
        string orderIdOrMpId,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(orderIdOrMpId))
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, "invalid_order_id");
        }

        try
        {
            var local = await _orderService.GetByExternalIdAsync(orderIdOrMpId, ct);
            if (local is not null)
            {
                var localIsFinal = IsFinalStatus(local.Status);
                if (localIsFinal || !LooksLikeMpOrderId(orderIdOrMpId))
                {
                    return ApiResult.Ok(
                        new CardOrderResponse
                        {
                            OrderId = local.OrderId,
                            ProviderOrderId = null,
                            ProviderPaymentId = local.ProviderPaymentIdText
                                ?? local.ProviderPaymentId?.ToString(
                                    CultureInfo.InvariantCulture
                                ),
                            Status = local.Status,
                            StatusDetail = local.StatusDetail,
                            IsFinal = localIsFinal,
                            Amount = local.Amount,
                        }
                    );
                }
            }
        }
        catch { }

        var client = CreateMpClient(GetAccessToken());
        var resp = await client.GetAsync(
            $"https://api.mercadopago.com/v1/orders/{orderIdOrMpId}",
            ct
        );
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            return BuildMpErrorResponse(resp, body);
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var providerOrderId = TryGetString(root, "id");
        var externalReference = TryGetString(root, "external_reference");
        var orderIdForResponse = externalReference ?? orderIdOrMpId;

        var orderStatus = TryGetString(root, "status");
        var orderStatusDetail = TryGetString(root, "status_detail");

        string? paymentStatus = null;
        string? paymentStatusDetail = null;
        string? paymentId = null;
        decimal? paymentAmount = null;

        if (TryGetFirstPayment(root, out var payment))
        {
            paymentStatus = TryGetString(payment, "status");
            paymentStatusDetail = TryGetString(payment, "status_detail");
            paymentId = TryGetString(payment, "id");
            if (TryGetDecimal(payment, "amount", out var amountValue))
            {
                paymentAmount = amountValue;
            }
        }

        if (!paymentAmount.HasValue && TryGetDecimal(root, "total_amount", out var totalAmount))
        {
            paymentAmount = totalAmount;
        }

        var status = paymentStatus ?? orderStatus;
        var statusDetail = paymentStatusDetail ?? orderStatusDetail;
        var normalized = NormalizeMpStatus(status);

        if (string.Equals(statusDetail, "expired", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "expired";
        }

        var isFinal = IsFinalStatus(normalized);
        var finalAmount = paymentAmount ?? 0m;

        if (!string.IsNullOrWhiteSpace(externalReference))
        {
            try
            {
                await _orderService.CreateOrUpdateAsync(
                    externalReference,
                    finalAmount,
                    "BRL",
                    "mercadopago",
                    TryParseLong(paymentId),
                    paymentId,
                    normalized ?? status ?? "pending",
                    statusDetail,
                    "card",
                    null,
                    ct
                );

                if (TryParsePaymentIntentId(externalReference, out var intentId))
                {
                    try
                    {
                        await _walletService.SyncDepositStatusAsync(
                            intentId,
                            status,
                            statusDetail,
                            paymentId ?? providerOrderId,
                            paymentAmount,
                            ct
                        );
                    }
                    catch { }
                }
            }
            catch { }
        }

        return ApiResult.Ok(
            new CardOrderResponse
            {
                OrderId = orderIdForResponse,
                ProviderOrderId = providerOrderId,
                ProviderPaymentId = paymentId,
                Status = normalized ?? "unknown",
                StatusDetail = statusDetail,
                IsFinal = isFinal,
                Amount = finalAmount,
            }
        );
    }

    public async Task<ApiResult> CreatePixAsync(
        CreatePixRequest req,
        string? deviceId,
        CancellationToken ct = default
    )
    {
        var client = CreateMpClient(GetAccessToken());

        client.DefaultRequestHeaders.Remove("X-Idempotency-Key");
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", req.OrderId);

        var expires = DateTimeOffset
            .UtcNow.AddMinutes(PixExpirationMinutes)
            .ToOffset(TimeSpan.FromHours(-3))
            .ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz");

        var payload = new Dictionary<string, object?>
        {
            ["transaction_amount"] = req.Amount,
            ["description"] = req.Description,
            ["payment_method_id"] = "pix",
            ["payer"] = new { email = req.BuyerEmail },
            ["external_reference"] = req.OrderId,
            ["date_of_expiration"] = expires,
            ["notification_url"] = _options.MP_WEBHOOK_URL,
        };

        var httpReq = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.mercadopago.com/v1/payments"
        )
        {
            Content = JsonContent.Create(payload),
        };

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            httpReq.Headers.TryAddWithoutValidation("X-meli-session-id", deviceId);
        }

        var resp = await client.SendAsync(httpReq, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            return BuildMpErrorResponse(resp, body);
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var paymentId = root.GetProperty("id").GetInt64();
        var status = root.TryGetProperty("status", out var st) ? st.GetString() : "unknown";
        var statusDetail = root.TryGetProperty("status_detail", out var sd) ? sd.GetString() : null;

        string? qrBase64 = null;
        string? qrCode = null;

        if (
            root.TryGetProperty("point_of_interaction", out var poi)
            && poi.TryGetProperty("transaction_data", out var td)
        )
        {
            if (td.TryGetProperty("qr_code_base64", out var b64))
            {
                qrBase64 = b64.GetString();
            }

            if (td.TryGetProperty("qr_code", out var qrc))
            {
                qrCode = qrc.GetString();
            }
        }

        DateTimeOffset? mpExpiresAt = null;
        if (
            root.TryGetProperty("date_of_expiration", out var exp)
            && exp.ValueKind == JsonValueKind.String
        )
        {
            if (DateTimeOffset.TryParse(exp.GetString(), out var dt))
            {
                mpExpiresAt = dt;
            }
        }

        var finalExpiresAt = (
            mpExpiresAt ?? DateTimeOffset.UtcNow.AddMinutes(PixExpirationMinutes)
        ).ToUniversalTime();

        try
        {
            var normalizedStatus = NormalizeMpStatus(status);

            if (string.Equals(statusDetail, "expired", StringComparison.OrdinalIgnoreCase))
            {
                normalizedStatus = "expired";
            }

            await _orderService.CreateOrUpdateAsync(
                req.OrderId,
                req.Amount,
                "BRL",
                "mercadopago",
                paymentId,
                paymentId.ToString(CultureInfo.InvariantCulture),
                normalizedStatus ?? "pending",
                statusDetail,
                "pix",
                finalExpiresAt,
                ct
            );
        }
        catch
        {
            // keep flow
        }

        return ApiResult.Ok(
            new PixResponse
            {
                PaymentId = paymentId,
                QrCodeBase64 = qrBase64,
                QrCode = qrCode,
                ExpiresAt = finalExpiresAt,
                Status = status ?? "unknown",
            }
        );
    }

    public async Task<ApiResult> SaveCardAsync(
        SaveCardRequest req,
        string? deviceId,
        CancellationToken ct = default
    )
    {
        if (
            req is null
            || string.IsNullOrWhiteSpace(req.Token)
            || req.Payer is null
            || string.IsNullOrWhiteSpace(req.Payer.Email)
        )
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, "invalid_request");
        }

        var client = CreateMpClient(GetAccessToken());
        var email = req.Payer.Email.Trim();
        var finalDeviceId = !string.IsNullOrWhiteSpace(deviceId) ? deviceId : req.DeviceId;

        var searchResp = await client.GetAsync(
            $"https://api.mercadopago.com/v1/customers/search?email={Uri.EscapeDataString(email)}",
            ct
        );
        var searchBody = await searchResp.Content.ReadAsStringAsync(ct);
        if (!searchResp.IsSuccessStatusCode)
        {
            return BuildMpErrorResponse(searchResp, searchBody);
        }

        string? customerId = null;
        try
        {
            using var doc = JsonDocument.Parse(searchBody);
            customerId = TryGetCustomerIdFromSearch(doc.RootElement);
        }
        catch { }

        if (string.IsNullOrWhiteSpace(customerId))
        {
            var createPayload = new Dictionary<string, object?> { ["email"] = email };

            AddPayerName(createPayload, req.Payer.CardholderName);

            if (
                req.Payer.Identification is not null
                && !string.IsNullOrWhiteSpace(req.Payer.Identification.Type)
                && !string.IsNullOrWhiteSpace(req.Payer.Identification.Number)
            )
            {
                createPayload["identification"] = new
                {
                    type = req.Payer.Identification.Type,
                    number = req.Payer.Identification.Number,
                };
            }

            var createReq = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.mercadopago.com/v1/customers"
            )
            {
                Content = JsonContent.Create(createPayload),
            };

            if (!string.IsNullOrWhiteSpace(finalDeviceId))
            {
                createReq.Headers.TryAddWithoutValidation("X-meli-session-id", finalDeviceId);
            }

            var createResp = await client.SendAsync(createReq, ct);
            var createBody = await createResp.Content.ReadAsStringAsync(ct);
            if (!createResp.IsSuccessStatusCode)
            {
                return BuildMpErrorResponse(createResp, createBody);
            }

            try
            {
                using var doc = JsonDocument.Parse(createBody);
                customerId = TryGetString(doc.RootElement, "id");
            }
            catch { }
        }

        if (string.IsNullOrWhiteSpace(customerId))
        {
            return ApiResult.Problem(StatusCodes.Status502BadGateway, "mp_customer_not_found");
        }

        var cardPayload = new Dictionary<string, object?> { ["token"] = req.Token };

        var cardReq = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.mercadopago.com/v1/customers/{customerId}/cards"
        )
        {
            Content = JsonContent.Create(cardPayload),
        };

        if (!string.IsNullOrWhiteSpace(finalDeviceId))
        {
            cardReq.Headers.TryAddWithoutValidation("X-meli-session-id", finalDeviceId);
        }

        var cardResp = await client.SendAsync(cardReq, ct);
        var cardBody = await cardResp.Content.ReadAsStringAsync(ct);
        if (!cardResp.IsSuccessStatusCode)
        {
            return BuildMpErrorResponse(cardResp, cardBody);
        }

        string? cardId = null;
        string? last4 = null;
        int? expMonth = null;
        int? expYear = null;
        string? mpPaymentMethodId = null;
        string? cardHolderName = null;

        try
        {
            using var doc = JsonDocument.Parse(cardBody);
            var root = doc.RootElement;
            cardId = TryGetString(root, "id");
            last4 = TryGetString(root, "last_four_digits");
            expMonth = TryGetInt(root, "expiration_month");
            expYear = TryGetInt(root, "expiration_year");

            if (root.TryGetProperty("payment_method", out var paymentMethod))
            {
                mpPaymentMethodId = TryGetString(paymentMethod, "id");
            }

            if (string.IsNullOrWhiteSpace(mpPaymentMethodId))
            {
                mpPaymentMethodId = TryGetString(root, "payment_method_id");
            }

            if (root.TryGetProperty("cardholder", out var cardholder))
            {
                cardHolderName = TryGetString(cardholder, "name");
            }
        }
        catch { }

        cardHolderName ??= req.Payer.CardholderName;
        var cardBrand = mpPaymentMethodId ?? req.PaymentMethodId;

        if (string.IsNullOrWhiteSpace(cardId))
        {
            return ApiResult.Problem(StatusCodes.Status502BadGateway, "mp_card_not_found");
        }

        return ApiResult.Ok(
            new SaveCardResponse
            {
                MpCustomerId = customerId,
                MpCardId = cardId,
                MpPaymentMethodId = mpPaymentMethodId,
                CardLast4 = last4,
                CardExpMonth = expMonth,
                CardExpYear = expYear,
                CardHolderName = cardHolderName,
                CardBrand = cardBrand,
            }
        );
    }

    public async Task<ApiResult> GetPaymentStatusAsync(
        long paymentId,
        CancellationToken ct = default
    )
    {
        try
        {
            var local = await _orderService.GetByProviderPaymentIdAsync(paymentId, ct);
            if (local is not null)
            {
                if (local.Status is "approved" or "rejected" or "cancelled" or "expired")
                {
                    return ApiResult.Ok(
                        new PaymentStatusResponse
                        {
                            PaymentId = paymentId,
                            Status = local.Status,
                            StatusDetail = local.StatusDetail,
                            ExternalReference = local.OrderId,
                            Amount = local.Amount,
                            ExpiresAt = local.ExpiresAtUtc,
                            IsFinal = true,
                        }
                    );
                }

                if (
                    local.ExpiresAtUtc.HasValue
                    && local.ExpiresAtUtc.Value <= DateTimeOffset.UtcNow
                )
                {
                    try
                    {
                        await _orderService.UpdateStatusByProviderIdAsync(
                            paymentId,
                            "expired",
                            null,
                            local.ExpiresAtUtc,
                            ct
                        );
                    }
                    catch { }

                    return ApiResult.Ok(
                        new PaymentStatusResponse
                        {
                            PaymentId = paymentId,
                            Status = "expired",
                            StatusDetail = local.StatusDetail,
                            ExternalReference = local.OrderId,
                            Amount = local.Amount,
                            ExpiresAt = local.ExpiresAtUtc,
                            IsFinal = true,
                        }
                    );
                }
            }
        }
        catch { }

        var client = CreateMpClient(GetAccessToken());

        var resp = await client.GetAsync(
            $"https://api.mercadopago.com/v1/payments/{paymentId}",
            ct
        );
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            return BuildMpErrorResponse(resp, body);
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var status = root.TryGetProperty("status", out var st) ? st.GetString() : "unknown";
        var statusDetail = root.TryGetProperty("status_detail", out var sd) ? sd.GetString() : null;
        var ext = root.TryGetProperty("external_reference", out var er) ? er.GetString() : null;

        decimal? amount = null;
        if (root.TryGetProperty("transaction_amount", out var ta) && ta.TryGetDecimal(out var a))
        {
            amount = a;
        }

        DateTimeOffset? expiresAt = null;
        if (
            root.TryGetProperty("date_of_expiration", out var exp)
            && exp.ValueKind == JsonValueKind.String
        )
        {
            if (DateTimeOffset.TryParse(exp.GetString(), out var dt))
            {
                expiresAt = dt.ToUniversalTime();
            }
        }

        var normalized = NormalizeMpStatus(status);

        if (string.Equals(statusDetail, "expired", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "expired";
        }

        if (
            string.Equals(normalized, "pending", StringComparison.OrdinalIgnoreCase)
            && expiresAt.HasValue
            && expiresAt.Value <= DateTimeOffset.UtcNow
        )
        {
            normalized = "expired";
        }

        var isFinal =
            string.Equals(normalized, "approved", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "rejected", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "expired", StringComparison.OrdinalIgnoreCase);

        if (isFinal)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(ext))
                {
                    await _orderService.CreateOrUpdateAsync(
                        ext,
                        amount ?? 0m,
                        "BRL",
                        "mercadopago",
                        paymentId,
                        paymentId.ToString(CultureInfo.InvariantCulture),
                        normalized,
                        statusDetail,
                        "pix",
                        expiresAt,
                        ct
                    );
                }
                else
                {
                    await _orderService.UpdateStatusByProviderIdAsync(
                        paymentId,
                        normalized,
                        statusDetail,
                        expiresAt,
                        ct
                    );
                }

                if (TryParsePaymentIntentId(ext, out var intentId))
                {
                    try
                    {
                        await _walletService.SyncDepositStatusAsync(
                            intentId,
                            status,
                            statusDetail,
                            paymentId.ToString(),
                            amount,
                            ct
                        );
                    }
                    catch { }
                }
            }
            catch { }
        }

        return ApiResult.Ok(
            new PaymentStatusResponse
            {
                PaymentId = paymentId,
                Status = normalized ?? "unknown",
                StatusDetail = statusDetail,
                ExternalReference = ext,
                Amount = amount,
                ExpiresAt = expiresAt,
                IsFinal = isFinal,
            }
        );
    }

    public async Task<ApiResult> HandleWebhookAsync(
        HttpRequest request,
        CancellationToken ct = default
    )
    {
        var raw = await new StreamReader(request.Body).ReadToEndAsync(ct);
        var topic = request.Query["topic"].ToString();
        if (string.IsNullOrWhiteSpace(topic))
        {
            topic = request.Query["type"].ToString();
        }

        if (
            string.Equals(topic, "order", StringComparison.OrdinalIgnoreCase)
            || string.Equals(topic, "orders", StringComparison.OrdinalIgnoreCase)
        )
        {
            return await HandleOrderWebhookAsync(request, raw, ct);
        }

        return await HandlePaymentWebhookAsync(request, raw, ct);
    }

    private async Task<ApiResult> HandlePaymentWebhookAsync(
        HttpRequest request,
        string raw,
        CancellationToken ct
    )
    {
        long? paymentId = null;
        if (long.TryParse(request.Query["data.id"], out var q1))
        {
            paymentId = q1;
        }
        else if (long.TryParse(request.Query["id"], out var q2))
        {
            paymentId = q2;
        }

        if (paymentId is null && !string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (
                    doc.RootElement.TryGetProperty("data", out var data)
                    && data.TryGetProperty("id", out var idEl)
                )
                {
                    if (
                        idEl.ValueKind == JsonValueKind.String
                        && long.TryParse(idEl.GetString(), out var v)
                    )
                    {
                        paymentId = v;
                    }
                    else if (idEl.ValueKind == JsonValueKind.Number && idEl.TryGetInt64(out var v2))
                    {
                        paymentId = v2;
                    }
                }
            }
            catch { }
        }

        var headers = string.Join(
            ";",
            request.Headers.Select(h => $"{h.Key}:{string.Join(',', h.Value!)}")
        );

        MPWebhookEvent? saved = null;
        try
        {
            saved = await _webhookService.SaveAsync(
                "mercadopago",
                "payment",
                paymentId,
                null,
                raw,
                headers,
                ct
            );
        }
        catch { }

        if (paymentId is null)
        {
            return ApiResult.Ok(null);
        }

        var token = GetWebhookAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return ApiResult.Ok(null);
        }

        var client = CreateMpClient(token);

        var resp = await client.GetAsync(
            $"https://api.mercadopago.com/v1/payments/{paymentId.Value}",
            ct
        );
        var paymentJson = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            if (saved is not null)
            {
                await _webhookService.MarkProcessedAsync(
                    saved.Id,
                    false,
                    $"MP GET failed: {resp.StatusCode}",
                    ct: ct
                );
            }

            return ApiResult.Ok(null);
        }

        using var paymentDoc = JsonDocument.Parse(paymentJson);
        var root = paymentDoc.RootElement;

        var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
        var statusDetail = root.TryGetProperty("status_detail", out var sd) ? sd.GetString() : null;
        var rejectionReason = root.TryGetProperty("rejection_reason", out var rr)
            ? rr.GetString()
            : null;
        var orderId = root.TryGetProperty("external_reference", out var er) ? er.GetString() : null;
        decimal? amount = null;
        if (
            root.TryGetProperty("transaction_amount", out var ta)
            && ta.TryGetDecimal(out var amountValue)
        )
        {
            amount = amountValue;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(orderId) && !string.IsNullOrWhiteSpace(status))
            {
                await _orderService.UpdateStatusAsync(
                    orderId!,
                    status!,
                    statusDetail,
                    paymentId,
                    paymentId?.ToString(CultureInfo.InvariantCulture),
                    ct
                );

                if (TryParsePaymentIntentId(orderId, out var intentId))
                {
                    try
                    {
                        await _walletService.SyncDepositStatusAsync(
                            intentId,
                            status,
                            statusDetail,
                            paymentId?.ToString(),
                            amount,
                            ct
                        );
                    }
                    catch { }
                }

                if (saved is not null)
                {
                    var msg = $"Order {orderId} updated to {status}";
                    if (
                        !string.IsNullOrWhiteSpace(statusDetail)
                        || !string.IsNullOrWhiteSpace(rejectionReason)
                    )
                    {
                        msg +=
                            $" (status_detail={statusDetail}, rejection_reason={rejectionReason})";
                    }

                    await _webhookService.MarkProcessedAsync(saved.Id, true, msg, orderId, ct);
                }
            }
            else
            {
                if (saved is not null)
                {
                    var msg = "Missing orderId or status in MP response";
                    if (
                        !string.IsNullOrWhiteSpace(statusDetail)
                        || !string.IsNullOrWhiteSpace(rejectionReason)
                    )
                    {
                        msg +=
                            $" (status_detail={statusDetail}, rejection_reason={rejectionReason})";
                    }

                    await _webhookService.MarkProcessedAsync(saved.Id, false, msg, orderId, ct);
                }
            }
        }
        catch (Exception ex)
        {
            if (saved is not null)
            {
                await _webhookService.MarkProcessedAsync(saved.Id, false, ex.Message, orderId, ct);
            }
        }

        return ApiResult.Ok(null);
    }

    private async Task<ApiResult> HandleOrderWebhookAsync(
        HttpRequest request,
        string raw,
        CancellationToken ct
    )
    {
        string? orderId = null;
        var dataId = request.Query["data.id"].ToString();
        if (!string.IsNullOrWhiteSpace(dataId))
        {
            orderId = dataId;
        }
        else
        {
            var idQuery = request.Query["id"].ToString();
            if (!string.IsNullOrWhiteSpace(idQuery))
            {
                orderId = idQuery;
            }
        }

        if (string.IsNullOrWhiteSpace(orderId) && !string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (
                    doc.RootElement.TryGetProperty("data", out var data)
                    && data.TryGetProperty("id", out var idEl)
                )
                {
                    if (idEl.ValueKind == JsonValueKind.String)
                    {
                        orderId = idEl.GetString();
                    }
                    else if (idEl.ValueKind == JsonValueKind.Number)
                    {
                        orderId = idEl.GetRawText();
                    }
                }
            }
            catch { }
        }

        var headers = string.Join(
            ";",
            request.Headers.Select(h => $"{h.Key}:{string.Join(',', h.Value!)}")
        );

        MPWebhookEvent? saved = null;
        try
        {
            saved = await _webhookService.SaveAsync(
                "mercadopago",
                "order",
                null,
                orderId,
                raw,
                headers,
                ct
            );
        }
        catch { }

        if (string.IsNullOrWhiteSpace(orderId))
        {
            return ApiResult.Ok(null);
        }

        var token = GetWebhookAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return ApiResult.Ok(null);
        }

        var client = CreateMpClient(token);
        var resp = await client.GetAsync($"https://api.mercadopago.com/v1/orders/{orderId}", ct);
        var orderJson = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            if (saved is not null)
            {
                await _webhookService.MarkProcessedAsync(
                    saved.Id,
                    false,
                    $"MP GET failed: {resp.StatusCode}",
                    ct: ct
                );
            }

            return ApiResult.Ok(null);
        }

        using var orderDoc = JsonDocument.Parse(orderJson);
        var root = orderDoc.RootElement;

        var externalReference = TryGetString(root, "external_reference");
        var orderStatus = TryGetString(root, "status");
        var orderStatusDetail = TryGetString(root, "status_detail");

        string? paymentStatus = null;
        string? paymentStatusDetail = null;
        string? paymentId = null;
        string? rejectionReason = null;
        decimal? amount = null;

        if (TryGetFirstPayment(root, out var payment))
        {
            paymentStatus = TryGetString(payment, "status");
            paymentStatusDetail = TryGetString(payment, "status_detail");
            paymentId = TryGetString(payment, "id");
            rejectionReason = TryGetString(payment, "rejection_reason");
            if (TryGetDecimal(payment, "amount", out var amountValue))
            {
                amount = amountValue;
            }
        }

        if (!amount.HasValue && TryGetDecimal(root, "total_amount", out var totalAmount))
        {
            amount = totalAmount;
        }

        var status = paymentStatus ?? orderStatus;
        var statusDetail = paymentStatusDetail ?? orderStatusDetail;
        var normalized = NormalizeMpStatus(status);

        if (string.Equals(statusDetail, "expired", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "expired";
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(externalReference))
            {
                await _orderService.CreateOrUpdateAsync(
                    externalReference,
                    amount ?? 0m,
                    "BRL",
                    "mercadopago",
                    TryParseLong(paymentId),
                    paymentId,
                    normalized ?? status ?? "pending",
                    statusDetail,
                    "card",
                    null,
                    ct
                );

                if (TryParsePaymentIntentId(externalReference, out var intentId))
                {
                    try
                    {
                        await _walletService.SyncDepositStatusAsync(
                            intentId,
                            status,
                            statusDetail,
                            paymentId ?? orderId,
                            amount,
                            ct
                        );
                    }
                    catch { }
                }

                if (saved is not null)
                {
                    var statusLabel = normalized ?? status ?? "unknown";
                    var msg = $"Order {externalReference} updated to {statusLabel}";
                    if (
                        !string.IsNullOrWhiteSpace(statusDetail)
                        || !string.IsNullOrWhiteSpace(rejectionReason)
                    )
                    {
                        msg +=
                            $" (status_detail={statusDetail}, rejection_reason={rejectionReason})";
                    }

                    await _webhookService.MarkProcessedAsync(
                        saved.Id,
                        true,
                        msg,
                        externalReference,
                        ct
                    );
                }
            }
            else
            {
                if (saved is not null)
                {
                    var msg = "Missing external_reference in MP order response";
                    if (
                        !string.IsNullOrWhiteSpace(statusDetail)
                        || !string.IsNullOrWhiteSpace(rejectionReason)
                    )
                    {
                        msg +=
                            $" (status_detail={statusDetail}, rejection_reason={rejectionReason})";
                    }

                    await _webhookService.MarkProcessedAsync(
                        saved.Id,
                        false,
                        msg,
                        externalReference,
                        ct
                    );
                }
            }
        }
        catch (Exception ex)
        {
            if (saved is not null)
            {
                await _webhookService.MarkProcessedAsync(
                    saved.Id,
                    false,
                    ex.Message,
                    externalReference,
                    ct
                );
            }
        }

        return ApiResult.Ok(null);
    }

    private ApiResult BuildMpErrorResponse(HttpResponseMessage resp, string body)
    {
        string? statusDetail = null;
        string? rejectionReason = null;

        try
        {
            using var doc = JsonDocument.Parse(body ?? string.Empty);
            var root = doc.RootElement;
            if (
                root.TryGetProperty("status_detail", out var sd)
                && sd.ValueKind == JsonValueKind.String
            )
            {
                statusDetail = sd.GetString();
            }

            if (
                root.TryGetProperty("rejection_reason", out var rr)
                && rr.ValueKind == JsonValueKind.String
            )
            {
                rejectionReason = rr.GetString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse Mercado Pago error body");
        }

        _logger.LogInformation(
            "MercadoPago returned {StatusCode} (status_detail={StatusDetail}, rejection_reason={RejectionReason}) body={Body}",
            (int)resp.StatusCode,
            statusDetail,
            rejectionReason,
            body
        );

        return ApiResult.ErrorBody(
            (int)resp.StatusCode,
            new
            {
                Error = "MercadoPagoError",
                StatusCode = (int)resp.StatusCode,
                StatusDetail = statusDetail,
                RejectionReason = rejectionReason,
                Raw = body,
            }
        );
    }

    private static string FormatAmount(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static void AddPayerName(Dictionary<string, object?> payer, string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return;
        }

        var parts = fullName
            .Trim()
            .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length > 0)
        {
            payer["first_name"] = parts[0];
        }

        if (parts.Length > 1)
        {
            payer["last_name"] = parts[1];
        }
    }

    private static bool TryGetFirstPayment(JsonElement root, out JsonElement payment)
    {
        payment = default;
        if (
            root.TryGetProperty("transactions", out var transactions)
            && transactions.TryGetProperty("payments", out var payments)
            && payments.ValueKind == JsonValueKind.Array
        )
        {
            foreach (var item in payments.EnumerateArray())
            {
                payment = item;
                return true;
            }
        }

        return false;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.TryGetInt64(out var l)
                ? l.ToString(CultureInfo.InvariantCulture)
            : prop.TryGetDecimal(out var d) ? d.ToString(CultureInfo.InvariantCulture)
            : prop.GetRawText(),
            _ => null,
        };
    }

    private static bool TryGetDecimal(JsonElement element, string propertyName, out decimal value)
    {
        value = default;
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return false;
        }

        if (prop.ValueKind == JsonValueKind.Number)
        {
            return prop.TryGetDecimal(out value);
        }

        if (prop.ValueKind == JsonValueKind.String)
        {
            return decimal.TryParse(
                prop.GetString(),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out value
            );
        }

        return false;
    }

    private static int? TryGetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var val))
        {
            return val;
        }

        if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var val2))
        {
            return val2;
        }

        return null;
    }

    private static string? TryGetCustomerIdFromSearch(JsonElement root)
    {
        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in results.EnumerateArray())
            {
                var id = TryGetString(item, "id");
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return id;
                }
            }
        }

        if (root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    var id = TryGetString(item, "id");
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        return id;
                    }
                }
            }
            else if (data.ValueKind == JsonValueKind.Object)
            {
                var id = TryGetString(data, "id");
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return id;
                }
            }
        }

        return null;
    }

    private static long? TryParseLong(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }

    private static string NormalizeMpStatus(string? mpStatus)
    {
        if (string.IsNullOrWhiteSpace(mpStatus))
        {
            return "unknown";
        }

        var s = mpStatus.Trim().ToLowerInvariant();

        return s switch
        {
            "approved" => "approved",
            "authorized" => "approved",
            "paid" => "approved",
            "processed" => "approved",
            "pending" => "pending",
            "in_process" => "pending",
            "processing" => "pending",
            "action_required" => "pending",
            "failed" => "rejected",
            "rejected" => "rejected",
            "refunded" => "rejected",
            "cancelled" => "rejected",
            "cancelled_by_user" => "rejected",
            "canceled" => "rejected",
            _ => s,
        };
    }

    private static string NormalizePaymentMethodId(string? value, string? paymentType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        var isDebit = string.Equals(paymentType, "debit_card", StringComparison.OrdinalIgnoreCase);

        if (isDebit)
        {
            if (normalized.StartsWith("deb", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            return normalized switch
            {
                "visa" => "debvisa",
                "mastercard" => "debmaster",
                "master" => "debmaster",
                "american express" => "debamex",
                "amex" => "debamex",
                "diners club" => "debdiners",
                "dinersclub" => "debdiners",
                "diners" => "debdiners",
                "hiper" => "debhipercard",
                "hipercard" => "debhipercard",
                "elo" => "debelo",
                _ => normalized,
            };
        }

        if (normalized.StartsWith("deb", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(3);
        }

        return normalized switch
        {
            "mastercard" => "master",
            "master" => "master",
            "american express" => "amex",
            "amex" => "amex",
            "diners club" => "diners",
            "dinersclub" => "diners",
            "diners" => "diners",
            "hiper" => "hipercard",
            _ => normalized,
        };
    }

    private static bool IsFinalStatus(string? normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return normalized.Equals("approved", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("rejected", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("expired", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("failed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeMpOrderId(string orderId)
    {
        return orderId.StartsWith("ORD", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParsePaymentIntentId(string? orderId, out Guid intentId)
    {
        intentId = default;
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return false;
        }

        if (Guid.TryParse(orderId, out intentId))
        {
            return true;
        }

        if (orderId.StartsWith(WalletDepositPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var rawId = orderId.Substring(WalletDepositPrefix.Length);
            return Guid.TryParse(rawId, out intentId);
        }

        return false;
    }

    private string GetAccessToken()
    {
        var live = _options.MP_ACCESS_TOKEN_LIVE;
        if (!string.IsNullOrWhiteSpace(live))
        {
            return live;
        }

        var test = _options.MP_ACCESS_TOKEN_TEST;
        if (!string.IsNullOrWhiteSpace(test))
        {
            return test;
        }

        throw new InvalidOperationException("MP token not configured (LIVE or TEST).");
    }

    private string GetWebhookAccessToken() => _options.MP_ACCESS_TOKEN_LIVE ?? string.Empty;

    private HttpClient CreateMpClient(string accessToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken
        );
        return client;
    }
}
