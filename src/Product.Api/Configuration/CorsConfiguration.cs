namespace Product.Api.Configuration;

public static class CorsConfiguration
{
    public static IServiceCollection AddApiCors(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var configured =
            configuration.GetSection("Cors:Allow").Get<string[]>() ?? Array.Empty<string>();

        services.AddCors(o =>
        {
            o.AddPolicy(
                "Allowlist",
                p =>
                    p.SetIsOriginAllowed(origin =>
                        {
                            if (string.IsNullOrWhiteSpace(origin))
                                return false;
                            try
                            {
                                var host = new Uri(origin).Host;
                                if (
                                    host.EndsWith(
                                        "ngrok-free.app",
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                                    return true;

                                return configured.Contains(
                                    origin,
                                    StringComparer.OrdinalIgnoreCase
                                );
                            }
                            catch
                            {
                                return false;
                            }
                        })
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()
            );
        });

        return services;
    }
}
