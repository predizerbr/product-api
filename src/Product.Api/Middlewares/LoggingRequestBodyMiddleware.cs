using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Product.Api.Middlewares;

public class LoggingRequestBodyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoggingRequestBodyMiddleware> _logger;

    public LoggingRequestBodyMiddleware(
        RequestDelegate next,
        ILogger<LoggingRequestBodyMiddleware> logger
    )
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only log for MP endpoints where request body is useful (pix)
        if (
            context.Request.Path.HasValue
            && context.Request.Path.Value!.Contains("/api/v1/payments/mercadopago/pix")
        )
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            if (!string.IsNullOrWhiteSpace(body))
            {
                _logger.LogInformation(
                    "[RequestBodyDump] Path={Path} Body={Body}",
                    context.Request.Path,
                    body
                );
            }
            else
            {
                _logger.LogInformation(
                    "[RequestBodyDump] Path={Path} Body is empty",
                    context.Request.Path
                );
            }
        }

        await _next(context);
    }
}
