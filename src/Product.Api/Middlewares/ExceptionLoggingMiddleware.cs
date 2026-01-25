namespace Product.Api.Middlewares;

public class ExceptionLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionLoggingMiddleware> _logger;

    public ExceptionLoggingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionLoggingMiddleware> logger
    )
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.Items.ContainsKey(CorrelationIdMiddleware.HeaderName)
                ? context.Items[CorrelationIdMiddleware.HeaderName]
                : context.TraceIdentifier;

            var userId = context
                .User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                ?.Value;
            var path = context.Request.Path;
            var method = context.Request.Method;

            _logger.LogError(
                ex,
                "Unhandled exception while processing request {Method} {Path} CorrelationId={CorrelationId} UserId={UserId}",
                method,
                path,
                correlationId,
                userId
            );

            throw;
        }
    }
}
