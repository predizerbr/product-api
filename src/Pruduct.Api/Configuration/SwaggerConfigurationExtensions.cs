using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.OpenApi.Models;

namespace Pruduct.Api.Configuration;

public static class SwaggerConfigurationExtensions
{
    public static IServiceCollection AddSwaggerWithBearer(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Pruduct API", Version = "v1" });

            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Insira o bearer token no formato: Bearer {token}",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = BearerTokenDefaults.AuthenticationScheme,
                BearerFormat = "Opaque",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = BearerTokenDefaults.AuthenticationScheme,
                },
            };

            c.AddSecurityDefinition(BearerTokenDefaults.AuthenticationScheme, securityScheme);
            c.AddSecurityRequirement(
                new OpenApiSecurityRequirement { { securityScheme, new List<string>() } }
            );
        });

        return services;
    }
}
