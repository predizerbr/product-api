using System.Net;
using System.Net.Mail;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Product.Api.Serialization;
using Product.Api.Services;
using Product.Business.BackgroundServices;
using Product.Business.Interfaces.Audit;
using Product.Business.Interfaces.Auth;
using Product.Business.Interfaces.Categories;
using Product.Business.Interfaces.Email;
using Product.Business.Interfaces.Market;
using Product.Business.Interfaces.Notifications;
using Product.Business.Interfaces.Payments;
using Product.Business.Interfaces.Portfolio;
using Product.Business.Interfaces.Users;
using Product.Business.Interfaces.Wallet;
using Product.Business.Options;
using Product.Business.Providers;
using Product.Business.Services.Audit;
using Product.Business.Services.Auth;
using Product.Business.Services.Mailers;
using Product.Business.Services.Markets;
using Product.Business.Services.Markets.Categories;
using Product.Business.Services.Payments;
using Product.Business.Services.Portfolio;
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
        services.AddApiCookiePolicy();
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
        services.AddSignalR();
        services.AddMemoryCache();

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
        services.Configure<RiskTermsCompanyOptions>(
            configuration.GetSection(RiskTermsCompanyOptions.SectionName)
        );
        services.Configure<MercadoPagoOptions>(configuration);
        services.Configure<MaintenanceOptions>(configuration.GetSection("Maintenance"));

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
            PasswordResetTokenProvider<ApplicationUser>.OptionsName,
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

    public static IServiceCollection AddApiCookiePolicy(this IServiceCollection services)
    {
        services.Configure<CookiePolicyOptions>(options =>
        {
            options.MinimumSameSitePolicy = SameSiteMode.None;
            options.Secure = CookieSecurePolicy.Always;
            options.HttpOnly = HttpOnlyPolicy.Always;

            options.OnAppendCookie = context => ApplyCrossSiteCookiePolicy(context.CookieOptions);
            options.OnDeleteCookie = context => ApplyCrossSiteCookiePolicy(context.CookieOptions);
        });

        return services;
    }

    public static IServiceCollection AddApiAppServices(this IServiceCollection services)
    {
        services.AddSingleton<IRiskTermsTemplateRepository, FileRiskTermsTemplateRepository>();
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
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<IMercadoPagoService, MercadoPagoService>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IEmailQueueRepository, EmailQueueRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IWebhookRepository, WebhookRepository>();
        services.AddScoped<IMercadoPagoRepository, MercadoPagoRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IPortfolioRepository, PortfolioRepository>();
        services.AddScoped<IDbMigrationRepository, DbMigrationRepository>();
        services.AddScoped<IMarketRepository, MarketRepository>();
        services.AddScoped<IRiskTermsRepository, RiskTermsRepository>();
        services.AddScoped<IMarketService, MarketService>();
        services.AddScoped<IRiskTermsService, RiskTermsService>();
        services.AddScoped<IRiskTermsPdfGenerator, RiskTermsPdfGenerator>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IMarketNotifier, MarketNotifier>();
        services.AddScoped<IRolePromotionService, RolePromotionService>();
        services.AddScoped<
            IUserClaimsPrincipalFactory<ApplicationUser>,
            AppClaimsPrincipalFactory
        >();

        services.AddHostedService<PersistentEmailBackgroundService>();
        services.AddHostedService<MaintenanceBackgroundService>();

        return services;
    }

    public static IServiceCollection AddApiAuth(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSingleton<ILookupNormalizer, DiacriticsLookupNormalizer>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher>();

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
            .AddIdentityCore<ApplicationUser>(options =>
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
                    PasswordResetTokenProvider<ApplicationUser>.ProviderName;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddTokenProvider<PasswordResetTokenProvider<ApplicationUser>>(
                PasswordResetTokenProvider<ApplicationUser>.ProviderName
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

        services.AddRolePolicies();

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

    private static void ApplyCrossSiteCookiePolicy(CookieOptions options)
    {
        options.SameSite = SameSiteMode.None;
        options.Secure = true;
        options.HttpOnly = true;
    }
}
