using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Product.Api.Middlewares;
using Serilog;

namespace Product.Api.Configuration;

public static class PipelineConfiguration
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ExceptionLoggingMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseSerilogRequestLogging();
        app.UseProblemDetails();
        app.UseCors("Allowlist");
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHealthChecks("/health");
        app.MapHealthChecks("/ready");
        app.MapControllers();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Product API v1");
                c.RoutePrefix = "swagger";
            });

            app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
        }
        else
        {
            app.MapGet("/", () => Results.Ok("Product API online")).ExcludeFromDescription();
        }

        _ = app.Lifetime.ApplicationStarted.Register(() =>
        {
            var server = app.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
            var activeAddresses =
                (addresses is not null && addresses.Count > 0) ? addresses : app.Urls;

            var addressList = string.Join(", ", activeAddresses);
            var swaggerList = app.Environment.IsDevelopment()
                ? string.Join(", ", activeAddresses.Select(u => $"{u.TrimEnd('/')}/swagger"))
                : "disabled";

            app.Logger.LogInformation("API listening on: {Urls}", addressList);
            app.Logger.LogInformation("Swagger UI: {SwaggerUrls}", swaggerList);
        });

        return app;
    }
}
