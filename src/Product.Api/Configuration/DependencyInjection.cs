using System.Net;
using System.Net.Mail;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Product.Api.Serialization;
using Product.Business.BackgroundServices;
using Product.Business.Interfaces.Audit;
using Product.Business.Interfaces.Auth;
using Product.Business.Interfaces.Email;
using Product.Business.Interfaces.Payments;
using Product.Business.Interfaces.Users;
using Product.Business.Interfaces.Wallet;
using Product.Business.Options;
using Product.Business.Providers;
using Product.Business.Services.Audit;
using Product.Business.Services.Auth;
using Product.Business.Services.Mailers;
using Product.Business.Services.Payments;
using Product.Business.Services.Users;
using Product.Business.Services.Wallet;
using Product.Business.Validators.Auth;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Users;
using Product.Data.Repositories;
using AuthEmail = Product.Business.Interfaces.Auth;
using Mailers = Product.Business.Services.Mailers;

namespace Product.Api.Configuration;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddApiControllers();
        services.AddApiOptions(configuration);
        services.AddApiAppServices();
        services.AddApiSwagger();
        services.AddApiCors(configuration);
        services.AddApiEmail(configuration);
        services.AddApiAuth(configuration);
        services.AddApiDb(configuration);

        return services;
    }

    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services
            .AddControllers()
            .AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.Converters.Add(new DecimalStringJsonConverter());
                options.SerializerSettings.Converters.Add(new NullableDecimalStringJsonConverter());
            });

        services.AddHttpContextAccessor();
        services.AddHttpClient();

        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssembly(typeof(SignupRequestValidator).Assembly);

        return services;
    }

    public static IServiceCollection AddApiOptions(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<IdentityTokenOptions>(
            configuration.GetSection(IdentityTokenOptions.SectionName)
        );
        services.Configure<EmailOptions>(configuration.GetSection("Email"));
        services.Configure<GoogleAuthOptions>(
            configuration.GetSection(GoogleAuthOptions.SectionName)
        );
        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));
        services.Configure<MercadoPagoOptions>(configuration);

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            var tokenOptions =
                configuration
                    .GetSection(IdentityTokenOptions.SectionName)
                    .Get<IdentityTokenOptions>() ?? new IdentityTokenOptions();

            options.TokenLifespan = TimeSpan.FromHours(
                tokenOptions.EmailConfirmationTokenExpirationInHours
            );
        });

        services.Configure<DataProtectionTokenProviderOptions>(
            PasswordResetTokenProvider<User>.OptionsName,
            options =>
            {
                var tokenOptions =
                    configuration
                        .GetSection(IdentityTokenOptions.SectionName)
                        .Get<IdentityTokenOptions>() ?? new IdentityTokenOptions();

                options.TokenLifespan = TimeSpan.FromMinutes(
                    tokenOptions.PasswordResetTokenExpirationInMinutes
                );
            }
        );

        return services;
    }

    public static IServiceCollection AddApiAppServices(this IServiceCollection services)
    {
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IEmailQueue, PersistentEmailQueue>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IEmailTemplateRenderer, RazorEmailTemplateRenderer>();
        services.AddScoped<QueuedEmailSender>();
        services.AddScoped<Mailers.IEmailSender>(sp => sp.GetRequiredService<QueuedEmailSender>());
        services.AddScoped<AuthEmail.IEmailSender>(sp =>
            sp.GetRequiredService<QueuedEmailSender>()
        );
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<IMercadoPagoService, MercadoPagoService>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IEmailQueueRepository, EmailQueueRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IWebhookRepository, WebhookRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IDbMigrationRepository, DbMigrationRepository>();

        services.AddHostedService<PersistentEmailBackgroundService>();

        return services;
    }

    public static IServiceCollection AddApiAuth(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSingleton<ILookupNormalizer, DiacriticsLookupNormalizer>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher>();

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = "IdentityOrBearer";
                options.DefaultAuthenticateScheme = "IdentityOrBearer";
                options.DefaultChallengeScheme = IdentityConstants.BearerScheme;
            })
            .AddPolicyScheme(
                "IdentityOrBearer",
                "Bearer or Cookie",
                options =>
                {
                    options.ForwardDefaultSelector = context =>
                    {
                        var authHeader = context.Request.Headers.Authorization.ToString();
                        return
                            !string.IsNullOrWhiteSpace(authHeader)
                            && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                            ? IdentityConstants.BearerScheme
                            : IdentityConstants.ApplicationScheme;
                    };
                }
            )
            .AddBearerToken(
                IdentityConstants.BearerScheme,
                options =>
                {
                    var tokenOptions =
                        configuration
                            .GetSection(IdentityTokenOptions.SectionName)
                            .Get<IdentityTokenOptions>() ?? new IdentityTokenOptions();

                    options.BearerTokenExpiration = TimeSpan.FromMinutes(
                        tokenOptions.BearerTokenExpirationInMinutes
                    );
                    options.RefreshTokenExpiration = TimeSpan.FromDays(
                        tokenOptions.RefreshTokenExpirationInDays
                    );
                }
            )
            .AddIdentityCookies();

        services
            .AddIdentityCore<User>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);

                options.Tokens.PasswordResetTokenProvider =
                    PasswordResetTokenProvider<User>.ProviderName;
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddTokenProvider<PasswordResetTokenProvider<User>>(
                PasswordResetTokenProvider<User>.ProviderName
            );

        services.ConfigureApplicationCookie(options =>
        {
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.None;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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

        return services;
    }

    public static IServiceCollection AddApiEmail(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var emailOptions =
            configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();

        if (string.IsNullOrWhiteSpace(emailOptions.Host))
            throw new InvalidOperationException(
                "Config Email:Host nao carregou. Verifique appsettings e ambiente."
            );

        services
            .AddFluentEmail(emailOptions.FromEmail, emailOptions.FromName)
            .AddRazorRenderer()
            .AddSmtpSender(() =>
            {
                var client = new SmtpClient(emailOptions.Host, emailOptions.Port)
                {
                    EnableSsl = emailOptions.UseStartTls,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(
                        emailOptions.Username,
                        emailOptions.Password
                    ),
                };

                return client;
            });

        return services;
    }

    public static IServiceCollection AddApiDb(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddDbContext<AppDbContext>(o =>
            o.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
        );

        services.AddHealthChecks();

        return services;
    }
}
