using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Product.Business.Interfaces.Auth;
using Product.Business.Interfaces.Results;
using Product.Business.Interfaces.Users;
using Product.Business.Options;
using Product.Business.Providers;
using Product.Common.Enums;
using Product.Contracts.Auth;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Auth;
using Product.Data.Models.Users;

namespace Product.Business.Services.Auth;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IUserRepository _userRepository;
    private readonly IRolePromotionService _rolePromotionService;
    private readonly IEmailSender _emailSender;
    private readonly IOptions<FrontendOptions> _frontendOptions;
    private readonly IOptionsMonitor<Microsoft.AspNetCore.Authentication.BearerToken.BearerTokenOptions> _bearerOptions;
    private readonly IOptions<Product.Business.Options.GoogleAuthOptions> _googleOptions;
    private readonly ILogger<AuthService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IUserRepository userRepository,
        IEmailSender emailSender,
        IOptions<FrontendOptions> frontendOptions,
        IOptionsMonitor<Microsoft.AspNetCore.Authentication.BearerToken.BearerTokenOptions> bearerOptions,
        IOptions<Product.Business.Options.GoogleAuthOptions> googleOptions,
        ILogger<AuthService> logger,
        IRolePromotionService rolePromotionService,
        IHttpContextAccessor httpContextAccessor
    )
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _frontendOptions = frontendOptions;
        _bearerOptions = bearerOptions;
        _googleOptions = googleOptions;
        _logger = logger;
        _rolePromotionService = rolePromotionService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ApiResult> SignUpApiAsync(SignupRequest request, CancellationToken ct)
    {
        await SignUpAsync(request, ct);
        return ApiResult.NoContent();
    }

    public async Task<ApiResult> SignInApiAsync(
        LoginRequest request,
        bool? useCookies,
        bool? useSessionCookies
    )
    {
        await SignInAsync(request, useCookies, useSessionCookies);
        // After signing in (cookie or not), also return bearer + refresh tokens as a fallback
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return ApiResult.Ok(null);

        var tokens = await CreateBearerTokensAsync(user);
        // If client requested cookies, store refresh token in HttpOnly cookie and do not expose it in JSON
        if (useCookies == true && _httpContextAccessor?.HttpContext != null)
        {
            try
            {
                var ctx = _httpContextAccessor.HttpContext;
                var options = _bearerOptions.Get(IdentityConstants.BearerScheme);
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Path = "/",
                };
                if (useSessionCookies != true)
                {
                    cookieOptions.Expires = DateTimeOffset.UtcNow.Add(
                        options.RefreshTokenExpiration
                    );
                }

                if (!string.IsNullOrWhiteSpace(tokens.refreshToken))
                    ctx.Response.Cookies.Append("refreshToken", tokens.refreshToken, cookieOptions);

                return ApiResult.Ok(null);
            }
            catch
            {
                // ignore cookie failures and fallback to returning tokens in body
            }
        }

        return ApiResult.Ok(
            new
            {
                tokens.accessToken,
                tokens.refreshToken,
                tokens.expiresIn,
            }
        );
    }

    public async Task<ApiResult> SignOutApiAsync()
    {
        await SignOutAsync();
        return ApiResult.NoContent();
    }

    public async Task<ApiResult> RefreshApiAsync(RefreshRequest request)
    {
        // If caller didn't include refresh token in body, try to read it from HttpOnly cookie
        if (
            (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
            && _httpContextAccessor?.HttpContext?.Request?.Cookies != null
            && _httpContextAccessor.HttpContext.Request.Cookies.TryGetValue(
                "refreshToken",
                out var cookieRefresh
            )
            && !string.IsNullOrWhiteSpace(cookieRefresh)
        )
        {
            request = new RefreshRequest { RefreshToken = cookieRefresh };
        }

        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_refresh_token");

        await RefreshAsync(request);
        return ApiResult.Ok(null);
    }

    public async Task<ApiResult> ConfirmEmailApiAsync(Guid userId, string code, string? newEmail)
    {
        await ConfirmEmailAsync(userId, code, newEmail);
        return ApiResult.Ok(null);
    }

    public async Task<ApiResult> ResendConfirmationEmailApiAsync(
        ResendConfirmationEmailRequest request,
        CancellationToken ct
    )
    {
        await ResendConfirmationEmailAsync(request, ct);
        return ApiResult.Ok(null);
    }

    public async Task<ApiResult> ResendResetCodeApiAsync(
        ForgotPasswordRequest request,
        CancellationToken ct
    )
    {
        await ForgotPasswordAsync(request, ct);
        return ApiResult.Ok(null);
    }

    public async Task<ApiResult> GoogleSignInApiAsync(
        GoogleLoginRequest request,
        bool? useCookies = null,
        bool? useSessionCookies = null
    )
    {
        try
        {
            await GoogleLoginAsync(request, useCookies, useSessionCookies);

            // Re-validate id token to obtain email and return tokens as fallback
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                request.IdToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_googleOptions.Value.ClientId],
                }
            );

            var email = payload?.Email;
            if (string.IsNullOrWhiteSpace(email))
                return ApiResult.Ok(null);

            var user = await _userManager.FindByEmailAsync(email);
            if (user is null)
                return ApiResult.Ok(null);

            var tokens = await CreateBearerTokensAsync(user);
            // If client requested cookies, store refresh token in HttpOnly cookie and do not expose it in JSON
            if (useCookies == true && _httpContextAccessor?.HttpContext != null)
            {
                try
                {
                    var ctx = _httpContextAccessor.HttpContext;
                    var options = _bearerOptions.Get(IdentityConstants.BearerScheme);
                    var cookieOptions = new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.None,
                        Path = "/",
                    };
                    if (useSessionCookies != true)
                    {
                        cookieOptions.Expires = DateTimeOffset.UtcNow.Add(
                            options.RefreshTokenExpiration
                        );
                    }

                    if (!string.IsNullOrWhiteSpace(tokens.refreshToken))
                        ctx.Response.Cookies.Append(
                            "refreshToken",
                            tokens.refreshToken,
                            cookieOptions
                        );

                    // When using cookies, do not return any bearer tokens in the response body.
                    return ApiResult.Ok(null);
                }
                catch
                {
                    // ignore cookie failures and fallback to returning tokens in body
                }
            }

            return ApiResult.Ok(
                new
                {
                    tokens.accessToken,
                    tokens.refreshToken,
                    tokens.expiresIn,
                }
            );
        }
        catch (UnauthorizedAccessException)
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_google_token");
        }
    }

    private async Task<(
        string accessToken,
        string refreshToken,
        int expiresIn
    )> CreateBearerTokensAsync(ApplicationUser user)
    {
        var options = _bearerOptions.Get(IdentityConstants.BearerScheme);

        var principal = await _signInManager.CreateUserPrincipalAsync(user);

        var accessProperties = new AuthenticationProperties
        {
            ExpiresUtc = DateTimeOffset.UtcNow.Add(options.BearerTokenExpiration),
            IsPersistent = false,
        };

        var accessTicket = new AuthenticationTicket(
            principal,
            accessProperties,
            IdentityConstants.BearerScheme
        );
        var accessToken = options.BearerTokenProtector.Protect(accessTicket);

        var refreshProperties = new AuthenticationProperties
        {
            ExpiresUtc = DateTimeOffset.UtcNow.Add(options.RefreshTokenExpiration),
            IsPersistent = false,
        };
        var refreshTicket = new AuthenticationTicket(
            principal,
            refreshProperties,
            IdentityConstants.BearerScheme
        );
        var refreshToken = options.RefreshTokenProtector.Protect(refreshTicket);

        var expiresIn = (int)options.BearerTokenExpiration.TotalSeconds;
        return (accessToken, refreshToken, expiresIn);
    }

    public async Task<ApiResult> ForgotPasswordApiAsync(
        ForgotPasswordRequest request,
        CancellationToken ct
    )
    {
        try
        {
            await ForgotPasswordAsync(request, ct);
            return ApiResult.Ok(null);
        }
        catch (InvalidOperationException ex)
            when (ex.Message == "external_account_cannot_change_password")
        {
            return ApiResult.Problem(
                StatusCodes.Status400BadRequest,
                "external_account_cannot_change_password"
            );
        }
    }

    public async Task<ApiResult> ResetPasswordApiAsync(
        ResetPasswordRequest request,
        CancellationToken ct
    )
    {
        try
        {
            await ResetPasswordAsync(request, ct);
            return ApiResult.Ok(null);
        }
        catch (InvalidOperationException ex)
            when (ex.Message == "external_account_cannot_change_password")
        {
            return ApiResult.Problem(
                StatusCodes.Status400BadRequest,
                "external_account_cannot_change_password"
            );
        }
        catch (ArgumentException ex) when (ex.Message == "invalid_reset_token")
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, "invalid_reset_token");
        }
        catch (ArgumentException ex)
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    public async Task<ApiResult> VerifyResetCodeApiAsync(VerifyResetCodeRequest request)
    {
        try
        {
            await VerifyResetCodeAsync(request);
            return ApiResult.Ok(null);
        }
        catch (KeyNotFoundException)
        {
            return ApiResult.Problem(StatusCodes.Status404NotFound, "user_not_found");
        }
        catch (ArgumentException ex) when (ex.Message == "invalid_reset_token")
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, "invalid_reset_token");
        }
        catch (InvalidOperationException ex)
            when (ex.Message == "external_account_cannot_change_password")
        {
            return ApiResult.Problem(
                StatusCodes.Status400BadRequest,
                "external_account_cannot_change_password"
            );
        }
        catch (ArgumentException ex)
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    public async Task<ApiResult> GetInfoApiAsync(ClaimsPrincipal principal)
    {
        var info = await GetInfoAsync(principal);
        return ApiResult.Ok(info);
    }

    public async Task<ApiResult> UpdateInfoApiAsync(
        ClaimsPrincipal principal,
        InfoRequest request,
        CancellationToken ct
    )
    {
        try
        {
            var info = await UpdateInfoAsync(principal, request, ct);
            return ApiResult.Ok(info);
        }
        catch (InvalidOperationException ex)
            when (ex.Message == "external_account_cannot_change_password")
        {
            return ApiResult.Problem(
                StatusCodes.Status400BadRequest,
                "external_account_cannot_change_password"
            );
        }
    }

    public async Task<ApiResult> GetTwoFactorApiAsync(ClaimsPrincipal principal)
    {
        var resp = await GetTwoFactorAsync(principal);
        return ApiResult.Ok(resp);
    }

    public async Task<ApiResult> UpdateTwoFactorApiAsync(
        ClaimsPrincipal principal,
        TwoFactorRequest request
    )
    {
        var resp = await UpdateTwoFactorAsync(principal, request);
        return ApiResult.Ok(resp);
    }

    public async Task<ApiResult> HasExternalLoginApiAsync(ClaimsPrincipal principal)
    {
        var providers = await GetExternalLoginProvidersAsync(principal);
        var has = providers != null && providers.Any();
        return ApiResult.Ok(new { hasExternalLogin = has, providers });
    }

    public async Task<ApiResult> ChangePasswordApiAsync(
        ClaimsPrincipal principal,
        ChangePasswordRequest request,
        CancellationToken ct
    )
    {
        try
        {
            await ChangePasswordAsync(principal, request, ct);
            return ApiResult.Ok(null);
        }
        catch (UnauthorizedAccessException)
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }
        catch (ArgumentException ex) when (ex.Message == "password_mismatch")
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, "password_mismatch");
        }
        catch (InvalidOperationException ex)
            when (ex.Message == "external_account_cannot_change_password")
        {
            return ApiResult.Problem(
                StatusCodes.Status400BadRequest,
                "external_account_cannot_change_password"
            );
        }
        catch (ArgumentException ex)
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    public async Task<bool> HasExternalLoginAsync(
        ClaimsPrincipal principal,
        string? provider = null
    )
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
            throw new UnauthorizedAccessException("invalid_token");

        var logins = await _userManager.GetLoginsAsync(user);
        if (logins is null || logins.Count == 0)
            return false;

        if (string.IsNullOrWhiteSpace(provider))
            return logins.Count > 0;

        return logins.Any(l =>
            string.Equals(l.LoginProvider, provider, StringComparison.OrdinalIgnoreCase)
        );
    }

    public async Task<IEnumerable<string>> GetExternalLoginProvidersAsync(ClaimsPrincipal principal)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
            throw new UnauthorizedAccessException("invalid_token");

        var logins = await _userManager.GetLoginsAsync(user);
        if (logins is null || logins.Count == 0)
            return Array.Empty<string>();

        return logins.Select(l => l.LoginProvider).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public async Task GoogleLoginAsync(
        GoogleLoginRequest request,
        bool? useCookies = null,
        bool? useSessionCookies = null
    )
    {
        if (request is null || string.IsNullOrWhiteSpace(request.IdToken))
            throw new ArgumentException("invalid_request");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _googleOptions.Value.ClientId },
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch
        {
            throw new UnauthorizedAccessException("invalid_google_token");
        }

        if (
            payload == null
            || string.IsNullOrWhiteSpace(payload.Email)
            || payload.EmailVerified != true
        )
            throw new UnauthorizedAccessException("invalid_google_token");

        var email = payload.Email;
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Create new user for Google sign-in
            var initialUserName = ExtractUsernameFromEmail(email);
            var normalizedUserName = NormalizeUsername(initialUserName);

            var candidate = normalizedUserName;
            var suffix = 1;
            while (await _userManager.FindByNameAsync(candidate) is not null)
            {
                candidate = normalizedUserName + suffix.ToString();
                suffix++;
            }

            normalizedUserName = candidate;
            var displayName = string.IsNullOrWhiteSpace(payload.Name)
                ? initialUserName
                : payload.Name;
            user = new ApplicationUser
            {
                Email = NormalizeEmail(email),
                UserName = normalizedUserName,
                Name = displayName,
                NormalizedUserName = NormalizedUserName(normalizedUserName),
                Status = "ACTIVE",
                EmailConfirmed = true,
                EmailVerifiedAt = DateTimeOffset.UtcNow,
            };

            // Create with random password (external login)
            var randomPassword = Guid.NewGuid().ToString("N") + "!Aa1";
            var createResult = await _userManager.CreateAsync(user, randomPassword);
            if (!createResult.Succeeded)
                throw new InvalidOperationException(
                    createResult.Errors.FirstOrDefault()?.Code ?? "google_signup_failed"
                );

            // roles are managed via the user's `Role` string property; no Identity role creation
            await _rolePromotionService.PromoteToRoleAsync(
                user.Id,
                RoleName.USER.ToString(),
                CancellationToken.None
            );

            await _userRepository.EnsurePersonalDataAsync(user.Id);
            // Ensure external login is associated when creating user via Google
            var loginInfo = new UserLoginInfo("Google", payload.Subject ?? string.Empty, "Google");
            var addLoginResult = await _userManager.AddLoginAsync(user, loginInfo);
            if (!addLoginResult.Succeeded)
            {
                // If adding external login fails, continue (user can still sign in), but log via exception
                throw new InvalidOperationException(
                    addLoginResult.Errors.FirstOrDefault()?.Code ?? "add_external_login_failed"
                );
            }
        }

        // If the user exists but does not have the external login, attach it now
        var existingLogins = await _userManager.GetLoginsAsync(user);
        if (
            !existingLogins.Any(l =>
                string.Equals(l.LoginProvider, "Google", StringComparison.OrdinalIgnoreCase)
                && l.ProviderKey == (payload.Subject ?? string.Empty)
            )
        )
        {
            var loginInfoExisting = new UserLoginInfo(
                "Google",
                payload.Subject ?? string.Empty,
                "Google"
            );
            var addLoginResultExisting = await _userManager.AddLoginAsync(user, loginInfoExisting);
            if (!addLoginResultExisting.Succeeded)
            {
                // ignore failure to add when user already exists; it's non-fatal for sign-in
            }
        }

        if (!string.Equals(user.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("user_inactive");

        var useCookieScheme = useCookies == true || useSessionCookies == true;
        var isPersistent = useCookies == true && useSessionCookies != true;
        _signInManager.AuthenticationScheme = useCookieScheme
            ? IdentityConstants.ApplicationScheme
            : IdentityConstants.BearerScheme;

        await _signInManager.SignInAsync(user, isPersistent);
    }

    public async Task SignUpAsync(SignupRequest request, CancellationToken ct)
    {
        if (
            string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.UserName)
            || string.IsNullOrWhiteSpace(request.Password)
        )
            throw new ArgumentException("invalid_request");

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            throw new ArgumentException("password_mismatch");

        if (await _userManager.FindByEmailAsync(request.Email) is not null)
            throw new ArgumentException("email_already_registered");

        // Always derive the username from the email local-part (ignore any client-supplied username).
        var initialUserName = ExtractUsernameFromEmail(request.Email);

        var normalizedUserName = NormalizeUsername(initialUserName);

        // Ensure uniqueness by appending a numeric suffix if needed.
        var candidate = normalizedUserName;
        var suffix = 1;
        while (await _userManager.FindByNameAsync(candidate) is not null)
        {
            candidate = normalizedUserName + suffix.ToString();
            suffix++;
        }

        normalizedUserName = candidate;

        var displayName = string.IsNullOrWhiteSpace(request.Name)
            ? request.UserName.Trim()
            : request.Name.Trim();

        var user = new ApplicationUser
        {
            Email = NormalizeEmail(request.Email),
            UserName = normalizedUserName,
            Name = displayName,
            NormalizedUserName = NormalizedUserName(normalizedUserName),
            Status = "ACTIVE",
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            throw new InvalidOperationException(
                createResult.Errors.FirstOrDefault()?.Code ?? "signup_failed"
            );

        // Set default role on user and persist via role promotion service
        await _rolePromotionService.PromoteToRoleAsync(user.Id, RoleName.USER.ToString(), ct);

        await _userRepository.EnsurePersonalDataAsync(user.Id, ct);

        await SendConfirmationEmailAsync(user, ct);
    }

    public async Task SignInAsync(LoginRequest request, bool? useCookies, bool? useSessionCookies)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            _logger.LogWarning("SignIn failed: user not found Email={Email}", request.Email);
            throw new UnauthorizedAccessException("invalid_credentials");
        }

        if (!string.Equals(user.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "SignIn failed: user inactive UserId={UserId} Email={Email}",
                user.Id,
                request.Email
            );
            throw new InvalidOperationException("user_inactive");
        }

        if (!user.EmailConfirmed)
        {
            _logger.LogWarning(
                "SignIn failed: email not confirmed UserId={UserId} Email={Email}",
                user.Id,
                request.Email
            );
            throw new InvalidOperationException("email_not_confirmed");
        }

        var useCookieScheme = useCookies == true || useSessionCookies == true;
        var isPersistent = useCookies == true && useSessionCookies != true;
        _signInManager.AuthenticationScheme = useCookieScheme
            ? IdentityConstants.ApplicationScheme
            : IdentityConstants.BearerScheme;

        var result = await _signInManager.PasswordSignInAsync(
            user,
            request.Password,
            isPersistent,
            lockoutOnFailure: true
        );
        if (result.RequiresTwoFactor)
            result = await HandleTwoFactorAsync(request, isPersistent);

        if (result.IsLockedOut)
        {
            _logger.LogWarning(
                "SignIn failed: user locked out UserId={UserId} Email={Email}",
                user.Id,
                request.Email
            );
            throw new InvalidOperationException("user_locked_out");
        }
        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "SignIn failed: invalid credentials UserId={UserId} Email={Email}",
                user.Id,
                request.Email
            );
            throw new UnauthorizedAccessException("invalid_credentials");
        }

        _logger.LogInformation(
            "SignIn success: UserId={UserId} Email={Email} Persistent={IsPersistent}",
            user.Id,
            request.Email,
            isPersistent
        );
    }

    public async Task SignOutAsync()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("SignOut executed");
    }

    public async Task RefreshAsync(RefreshRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new UnauthorizedAccessException("invalid_refresh_token");

        var options = _bearerOptions.Get(IdentityConstants.BearerScheme);
        var refreshTicket = options.RefreshTokenProtector.Unprotect(request.RefreshToken);
        var expiresUtc = refreshTicket?.Properties?.ExpiresUtc;
        if (refreshTicket is null || expiresUtc is null || expiresUtc <= DateTimeOffset.UtcNow)
            throw new UnauthorizedAccessException("invalid_refresh_token");

        var principal = refreshTicket.Principal;
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("invalid_refresh_token");

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            throw new UnauthorizedAccessException("invalid_refresh_token");

        if (await _signInManager.ValidateSecurityStampAsync(principal) is null)
            throw new UnauthorizedAccessException("invalid_refresh_token");

        _signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;
        await _signInManager.SignInAsync(user, isPersistent: false);
    }

    public async Task ConfirmEmailAsync(Guid userId, string code, string? newEmail)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            throw new KeyNotFoundException("user_not_found");
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("invalid_token");

        string decodedCode = TryDecodeTokenLenient(code);

        IdentityResult result = !string.IsNullOrWhiteSpace(newEmail)
            ? await _userManager.ChangeEmailAsync(user, newEmail, decodedCode)
            : await _userManager.ConfirmEmailAsync(user, decodedCode);

        if (!result.Succeeded)
        {
            // Log identity errors for debugging (do not log the token itself)
            try
            {
                var errors = string.Join(';', result.Errors.Select(e => e.Description));
                _logger.LogWarning(
                    "ConfirmEmail failed for user {UserId}: {Errors}",
                    userId,
                    errors
                );
            }
            catch { }

            throw new ArgumentException("invalid_token");
        }

        if (!user.EmailConfirmed)
            user.EmailConfirmed = true;
        user.EmailVerifiedAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);
    }

    private static string TryDecodeTokenLenient(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("invalid_token");

        // Try base64url first (default Identity encoding)
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch
        {
            // Fallback: try regular base64 with padding and char replacements
            try
            {
                var s = code.Replace('-', '+').Replace('_', '/');
                switch (s.Length % 4)
                {
                    case 2:
                        s += "==";
                        break;
                    case 3:
                        s += "=";
                        break;
                }
                var bytes = Convert.FromBase64String(s);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                throw new ArgumentException("invalid_token");
            }
        }
    }

    public async Task ResendConfirmationEmailAsync(
        ResendConfirmationEmailRequest request,
        CancellationToken ct
    )
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || user.EmailConfirmed)
            return;
        await SendConfirmationEmailAsync(user, ct);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return;
        // If the account was created via external provider (e.g. Google), do not send reset email
        var logins = await _userManager.GetLoginsAsync(user);
        if (
            logins != null
            && logins.Any(l =>
                string.Equals(l.LoginProvider, "Google", StringComparison.OrdinalIgnoreCase)
            )
        )
            throw new InvalidOperationException("external_account_cannot_change_password");
        var resetCode = await _userManager.GeneratePasswordResetTokenAsync(user);
        await _emailSender.SendForgotPasswordAsync(
            user.Email ?? request.Email,
            user.UserName ?? string.Empty,
            resetCode,
            ct
        );
    }

    public async Task VerifyResetCodeAsync(VerifyResetCodeRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            throw new KeyNotFoundException("user_not_found");

        // If the account is external (Google), do not allow reset code verification
        var logins = await _userManager.GetLoginsAsync(user);
        if (
            logins != null
            && logins.Any(l =>
                string.Equals(l.LoginProvider, "Google", StringComparison.OrdinalIgnoreCase)
            )
        )
            throw new InvalidOperationException("external_account_cannot_change_password");

        var isValid = await _userManager.VerifyUserTokenAsync(
            user,
            PasswordResetTokenProvider<ApplicationUser>.ProviderName,
            "ResetPassword",
            request.ResetCode
        );

        if (!isValid)
            throw new ArgumentException("invalid_reset_token");
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            throw new ArgumentException("password_mismatch");

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            throw new ArgumentException("invalid_reset_token");

        // Block resets for external accounts
        var logins = await _userManager.GetLoginsAsync(user);
        if (
            logins != null
            && logins.Any(l =>
                string.Equals(l.LoginProvider, "Google", StringComparison.OrdinalIgnoreCase)
            )
        )
            throw new InvalidOperationException("external_account_cannot_change_password");

        var result = await _userManager.ResetPasswordAsync(
            user,
            request.ResetCode,
            request.Password
        );
        if (!result.Succeeded)
            throw new ArgumentException("invalid_reset_token");

        await _emailSender.SendResetPasswordConfirmationAsync(
            user.Email ?? request.Email,
            user.UserName ?? string.Empty,
            ct
        );
    }

    public async Task ChangePasswordAsync(
        ClaimsPrincipal principal,
        ChangePasswordRequest request,
        CancellationToken ct
    )
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
            throw new UnauthorizedAccessException("invalid_token");

        // If the account is linked to an external provider (e.g. Google), disallow password changes
        if (await HasExternalLoginAsync(principal, "Google"))
            throw new InvalidOperationException("external_account_cannot_change_password");

        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
            throw new ArgumentException("password_mismatch");

        var result = await _userManager.ChangePasswordAsync(
            user,
            request.OldPassword,
            request.NewPassword
        );
        if (!result.Succeeded)
            throw new ArgumentException(result.Errors.FirstOrDefault()?.Code ?? "invalid_password");

        await _emailSender.SendResetPasswordConfirmationAsync(
            user.Email ?? string.Empty,
            user.UserName ?? string.Empty,
            ct
        );
    }

    public async Task<InfoResponse> GetInfoAsync(ClaimsPrincipal principal)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
        {
            try
            {
                var isAuth = principal?.Identity?.IsAuthenticated ?? false;
                _logger?.LogWarning("GetInfoAsync: principal.IsAuthenticated={IsAuth}", isAuth);
                var nameId = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                _logger?.LogWarning(
                    "GetInfoAsync: Claim NameIdentifier={NameId}",
                    nameId ?? "(null)"
                );
                var claims = principal?.Claims?.Select(c => new { c.Type, c.Value }).ToList();
                if (claims != null && claims.Count > 0)
                    _logger?.LogWarning(
                        "GetInfoAsync: Claims={Claims}",
                        JsonSerializer.Serialize(claims)
                    );
                else
                    _logger?.LogWarning("GetInfoAsync: No claims present on principal");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while logging principal info");
            }

            throw new UnauthorizedAccessException("invalid_token");
        }

        return new InfoResponse { Email = user.Email, IsEmailConfirmed = user.EmailConfirmed };
    }

    public async Task<InfoResponse> UpdateInfoAsync(
        ClaimsPrincipal principal,
        InfoRequest request,
        CancellationToken ct
    )
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
            throw new UnauthorizedAccessException("invalid_token");

        if (!string.IsNullOrWhiteSpace(request.NewEmail))
        {
            if (await _userManager.FindByEmailAsync(request.NewEmail) is not null)
                throw new ArgumentException("email_taken");

            var token = await _userManager.GenerateChangeEmailTokenAsync(user, request.NewEmail);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var confirmUrl = BuildConfirmEmailUrl(_frontendOptions.Value.BaseUrl, user.Id, code);
            await _emailSender.SendChangeEmailAsync(
                request.NewEmail,
                user.UserName ?? string.Empty,
                confirmUrl,
                ct
            );
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            // Disallow password change for users authenticated via external providers
            if (await HasExternalLoginAsync(principal, "Google"))
                throw new InvalidOperationException("external_account_cannot_change_password");

            if (string.IsNullOrWhiteSpace(request.OldPassword))
                throw new ArgumentException("old_password_required");
            var result = await _userManager.ChangePasswordAsync(
                user,
                request.OldPassword,
                request.NewPassword
            );
            if (!result.Succeeded)
                throw new ArgumentException("invalid_password");
        }

        return new InfoResponse { Email = user.Email, IsEmailConfirmed = user.EmailConfirmed };
    }

    public async Task<TwoFactorResponse> GetTwoFactorAsync(ClaimsPrincipal principal)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
            throw new UnauthorizedAccessException("invalid_token");

        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var isEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
        var isRemembered = await _signInManager.IsTwoFactorClientRememberedAsync(user);
        var recoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user);

        return new TwoFactorResponse
        {
            SharedKey = key,
            RecoveryCodesLeft = recoveryCodesLeft,
            IsTwoFactorEnabled = isEnabled,
            IsMachineRemembered = isRemembered,
        };
    }

    public async Task<TwoFactorResponse> UpdateTwoFactorAsync(
        ClaimsPrincipal principal,
        TwoFactorRequest request
    )
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
            throw new UnauthorizedAccessException("invalid_token");

        if (request.ForgetMachine == true)
            await _signInManager.ForgetTwoFactorClientAsync();
        if (request.ResetSharedKey == true)
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            await _userManager.SetTwoFactorEnabledAsync(user, false);
        }

        if (request.Enable == true)
        {
            if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
                throw new ArgumentException("two_factor_code_required");
            var verificationCode = request.TwoFactorCode.Replace(" ", string.Empty);
            var isValid = await _userManager.VerifyTwoFactorTokenAsync(
                user,
                TokenOptions.DefaultAuthenticatorProvider,
                verificationCode
            );
            if (!isValid)
                throw new ArgumentException("invalid_two_factor_code");
            await _userManager.SetTwoFactorEnabledAsync(user, true);
        }
        else if (request.Enable == false)
        {
            await _userManager.SetTwoFactorEnabledAsync(user, false);
        }

        string[] recoveryCodes = Array.Empty<string>();
        if (request.ResetRecoveryCodes == true || request.Enable == true)
        {
            var generated = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            recoveryCodes = generated?.ToArray() ?? Array.Empty<string>();
        }

        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        var isEnabledNow = await _userManager.GetTwoFactorEnabledAsync(user);
        var isRememberedNow = await _signInManager.IsTwoFactorClientRememberedAsync(user);
        var recoveryCodesLeftNow = await _userManager.CountRecoveryCodesAsync(user);

        return new TwoFactorResponse
        {
            SharedKey = key,
            RecoveryCodesLeft = recoveryCodesLeftNow,
            RecoveryCodes = recoveryCodes,
            IsTwoFactorEnabled = isEnabledNow,
            IsMachineRemembered = isRememberedNow,
        };
    }

    // Helpers copied from endpoints
    private static string BuildConfirmEmailUrl(string? baseUrl, Guid userId, string code)
    {
        var normalizedBase = string.IsNullOrWhiteSpace(baseUrl)
            ? string.Empty
            : baseUrl.TrimEnd('/');
        return string.IsNullOrWhiteSpace(normalizedBase)
            ? string.Empty
            : $"{normalizedBase}/confirm-email/{userId}/{code}";
    }

    private static string NormalizeEmail(string email) =>
        RemoveDiacritics(email).Trim().ToLowerInvariant();

    private static string NormalizedUserName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        return RemoveDiacritics(trimmed);
    }

    private static string NormalizeUsername(string username) =>
        RemoveDiacritics(username).Trim().ToLowerInvariant();

    private static string ExtractUsernameFromEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
            return NormalizeUsername(email);

        var localPart = email[..atIndex];
        return NormalizeUsername(localPart);
    }

    private static string RemoveDiacritics(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var normalized = value.Normalize(NormalizationForm.FormD);
        Span<char> buffer = stackalloc char[normalized.Length];
        var idx = 0;
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                buffer[idx++] = c;
        }
        return new string(buffer[..idx]).Normalize(NormalizationForm.FormC);
    }

    // Role management via Identity is disabled; roles are stored in the user's `Role` string.

    private async Task SendConfirmationEmailAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var confirmUrl = BuildConfirmEmailUrl(_frontendOptions.Value.BaseUrl, user.Id, code);
        await _emailSender.SendEmailVerificationAsync(
            user.Email ?? string.Empty,
            user.UserName ?? string.Empty,
            confirmUrl,
            ct
        );
    }

    private async Task<SignInResult> HandleTwoFactorAsync(LoginRequest request, bool isPersistent)
    {
        if (!string.IsNullOrWhiteSpace(request.TwoFactorRecoveryCode))
            return await _signInManager.TwoFactorRecoveryCodeSignInAsync(
                request.TwoFactorRecoveryCode
            );
        if (!string.IsNullOrWhiteSpace(request.TwoFactorCode))
        {
            var code = request.TwoFactorCode.Replace(" ", string.Empty);
            return await _signInManager.TwoFactorAuthenticatorSignInAsync(
                code,
                isPersistent,
                false
            );
        }
        return SignInResult.Failed;
    }
}
