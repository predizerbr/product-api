namespace Product.Api.Configuration;

public static class CorsConfiguration
{
    public static IServiceCollection AddApiCors(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var configured = new[]
        {
            "http://localhost:3000",
            "http://localhost:5280",
            "https://genuine-husky-secondly.ngrok-free.app",
            "https://cladocarpous-cryptogrammatical-yasmin.ngrok-free.dev",
            "https://predizer.com.br",
            "https://www.predizer.com.br",
            "https://predizerapi.onrender.com",
        };

        services.AddCors(o =>
        {
            o.AddPolicy(
                "Allowlist",
                p => p.WithOrigins(configured).AllowAnyHeader().AllowAnyMethod().AllowCredentials()
            );
        });

        return services;
    }
}
