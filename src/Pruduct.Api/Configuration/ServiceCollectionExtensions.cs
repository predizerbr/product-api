using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pruduct.Business.Abstractions;
using Pruduct.Business.Options;
using Pruduct.Business.Services;
using Pruduct.Business.Validators;
using ProblemDetailsExtensions = Hellang.Middleware.ProblemDetails.ProblemDetailsExtensions;

namespace Pruduct.Api.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddControllers().AddNewtonsoftJson();

        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssembly(typeof(SignupRequestValidator).Assembly);

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<GoogleAuthOptions>(
            configuration.GetSection(GoogleAuthOptions.SectionName)
        );
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IEmailSender, LoggingEmailSender>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

        ProblemDetailsExtensions.AddProblemDetails(services);
        services.AddSwaggerWithJwt();

        services.AddCors(o =>
            o.AddPolicy(
                "Allowlist",
                p =>
                    p.WithOrigins(
                            configuration.GetSection("Cors:Allow").Get<string[]>()
                                ?? Array.Empty<string>()
                        )
                        .AllowAnyHeader()
                        .AllowAnyMethod()
            )
        );

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwt =
                    configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                    ?? new JwtOptions();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                "RequireAdminL1",
                p => p.RequireRole("ADMIN_L1", "ADMIN_L2", "ADMIN_L3")
            );
            options.AddPolicy("RequireAdminL2", p => p.RequireRole("ADMIN_L2", "ADMIN_L3"));
            options.AddPolicy("RequireAdminL3", p => p.RequireRole("ADMIN_L3"));
        });

        services.AddDbContext<Data.Database.Contexts.AppDbContext>(o =>
            o.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
        );

        services.AddHealthChecks();

        return services;
    }
}
