using System.Diagnostics;

namespace Product.Api.Middlewares;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path + context.Request.QueryString;
        var correlationId = context.Items.ContainsKey(CorrelationIdMiddleware.HeaderName)
            ? context.Items[CorrelationIdMiddleware.HeaderName]
            : context.TraceIdentifier;
        var userId = context
            .User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?.Value;

        _logger.LogInformation(
            "Request start: {Method} {Path} CorrelationId={CorrelationId} UserId={UserId}",
            method,
            path,
            correlationId,
            userId
        );

        await _next(context);

        sw.Stop();
        var status = context.Response?.StatusCode;
        _logger.LogInformation(
            "Request end: {Method} {Path} Status={Status} ElapsedMs={Elapsed} CorrelationId={CorrelationId} UserId={UserId}",
            method,
            path,
            status,
            sw.ElapsedMilliseconds,
            correlationId,
            userId
        );
    }
}
