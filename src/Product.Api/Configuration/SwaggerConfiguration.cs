using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.OpenApi.Models;
using ProblemDetailsExtensions = Hellang.Middleware.ProblemDetails.ProblemDetailsExtensions;

namespace Product.Api.Configuration;

public static class SwaggerConfiguration
{
    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        ProblemDetailsExtensions.AddProblemDetails(services);
        services.AddSwaggerWithBearer();
        return services;
    }

    public static IServiceCollection AddSwaggerWithBearer(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Product API", Version = "v1" });

            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Insira o bearer token no formato: Bearer {token}",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            };

            // Use a conventional 'Bearer' definition id so Swagger UI recognizes the scheme
            c.AddSecurityDefinition("Bearer", securityScheme);
            c.AddSecurityRequirement(
                new OpenApiSecurityRequirement { { securityScheme, new List<string>() } }
            );
        });

        return services;
    }
}
