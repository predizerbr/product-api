using System.Globalization;
using System.Text;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Pruduct.Business.Abstractions;
using Pruduct.Business.Abstractions.Results;
using Pruduct.Business.Options;
using Pruduct.Common.Enums;
using Pruduct.Contracts.Auth;
using Pruduct.Data.Database.Contexts;
using Pruduct.Data.Models;
using Pruduct.Contracts.Users;

namespace Pruduct.Business.Services;

public class AuthService : IAuthService
{
    private static readonly TimeSpan PasswordResetTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan EmailVerificationTtl = TimeSpan.FromDays(2);

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IAuditService _auditService;
    private readonly IEmailSender _emailSender;
    private readonly JwtOptions _jwtOptions;
    private readonly GoogleAuthOptions _googleOptions;

    public AuthService(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IAuditService auditService,
        IEmailSender emailSender,
        Microsoft.Extensions.Options.IOptions<JwtOptions> jwtOptions,
        Microsoft.Extensions.Options.IOptions<GoogleAuthOptions> googleOptions
    )
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _auditService = auditService;
        _emailSender = emailSender;
        _jwtOptions = jwtOptions.Value;
        _googleOptions = googleOptions.Value;
    }

    public async Task<ServiceResult<AuthResponse>> SignupAsync(
        SignupRequest request,
        CancellationToken ct = default
    )
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        var emailExists = await _db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);
        if (emailExists)
        {
            return ServiceResult<AuthResponse>.Fail("email_already_registered");
        }

        var username = ExtractUsernameFromEmail(normalizedEmail);
        var normalizedName = NormalizeName(request.Name);

        var user = new User
        {
            Email = normalizedEmail,
            NormalizedEmail = normalizedEmail,
            Username = username,
            NormalizedUsername = username,
            Name = request.Name,
            NormalizedName = normalizedName,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Status = "ACTIVE",
            PersonalData = null,
        };

        var userRole = await EnsureRoleAsync(RoleName.USER, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleName = userRole.Name });
        await _db.SaveChangesAsync(ct);

        var verifyToken = await CreateEmailVerificationTokenAsync(user, ct);
        var tokens = await IssueTokensAsync(user, ct);

        await tx.CommitAsync(ct);

        await _auditService.LogAsync(user.Id, "AUTH_SIGNUP", "User", user.Id, ct: ct);
        await _emailSender.SendEmailVerificationAsync(user.Email, verifyToken, ct);

        return ServiceResult<AuthResponse>.Ok(tokens);
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken ct = default
    )
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (user is null)
        {
            return ServiceResult<AuthResponse>.Fail("invalid_credentials");
        }

        var validPassword = _passwordHasher.Verify(user.PasswordHash, request.Password);
        if (!validPassword)
        {
            return ServiceResult<AuthResponse>.Fail("invalid_credentials");
        }

        if (!string.Equals(user.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<AuthResponse>.Fail("user_inactive");
        }

        var tokens = await IssueTokensAsync(user, ct);

        await _auditService.LogAsync(user.Id, "AUTH_LOGIN", "User", user.Id, ct: ct);

        return ServiceResult<AuthResponse>.Ok(tokens);
    }

    public async Task<ServiceResult<AuthResponse>> RefreshAsync(
        RefreshRequest request,
        CancellationToken ct = default
    )
    {
        var hashed = _tokenService.HashRefreshToken(request.RefreshToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hashed, ct);
        if (existing is null)
        {
            return ServiceResult<AuthResponse>.Fail("invalid_refresh_token");
        }

        if (existing.RevokedAt is not null || existing.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return ServiceResult<AuthResponse>.Fail("expired_refresh_token");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == existing.UserId, ct);
        if (user is null)
        {
            return ServiceResult<AuthResponse>.Fail("user_not_found");
        }

        existing.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var tokens = await IssueTokensAsync(user, ct);

        await _auditService.LogAsync(user.Id, "AUTH_REFRESH", "User", user.Id, ct: ct);

        return ServiceResult<AuthResponse>.Ok(tokens);
    }

    public Task<ServiceResult<AuthResponse>> LoginWithGoogleAsync(
        GoogleLoginRequest request,
        CancellationToken ct = default
    )
    {
        return LoginWithGoogleInternalAsync(request, ct);
    }

    private async Task<ServiceResult<AuthResponse>> LoginWithGoogleInternalAsync(
        GoogleLoginRequest request,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(_googleOptions.ClientId))
        {
            return ServiceResult<AuthResponse>.Fail("google_not_configured");
        }

        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return ServiceResult<AuthResponse>.Fail("invalid_google_token");
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                request.IdToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _googleOptions.ClientId },
                }
            );
        }
        catch
        {
            return ServiceResult<AuthResponse>.Fail("invalid_google_token");
        }

        var email = payload.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return ServiceResult<AuthResponse>.Fail("invalid_google_token");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            var name = string.IsNullOrWhiteSpace(payload.Name)
                ? payload.GivenName ?? email
                : payload.Name;
            var username = ExtractUsernameFromEmail(email);

            user = new User
            {
                Email = email,
                NormalizedEmail = email,
                Username = username,
                NormalizedUsername = username,
                Name = name,
                NormalizedName = NormalizeName(name),
                PasswordHash = _passwordHasher.Hash(_tokenService.GenerateRefreshToken()),
                Status = "ACTIVE",
                EmailVerifiedAt = DateTimeOffset.UtcNow,
                AvatarUrl = payload.Picture,
                PersonalData = null,
            };

            var userRole = await EnsureRoleAsync(RoleName.USER, ct);

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);

            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleName = userRole.Name });
            await _db.SaveChangesAsync(ct);

            var tokens = await IssueTokensAsync(user, ct);

            await tx.CommitAsync(ct);

            await _auditService.LogAsync(user.Id, "AUTH_GOOGLE_SIGNUP", "User", user.Id, ct: ct);

            return ServiceResult<AuthResponse>.Ok(tokens);
        }

        if (!string.Equals(user.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<AuthResponse>.Fail("user_inactive");
        }

        if (user.EmailVerifiedAt is null)
        {
            user.EmailVerifiedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        var result = await IssueTokensAsync(user, ct);
        await _auditService.LogAsync(user.Id, "AUTH_GOOGLE_LOGIN", "User", user.Id, ct: ct);

        return ServiceResult<AuthResponse>.Ok(result);
    }

    public async Task<ServiceResult<bool>> LogoutAsync(
        Guid userId,
        LogoutRequest request,
        CancellationToken ct = default
    )
    {
        var hashed = _tokenService.HashRefreshToken(request.RefreshToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hashed, ct);

        if (existing is not null && existing.RevokedAt is null)
        {
            existing.RevokedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        if (existing is not null)
        {
            await _auditService.LogAsync(
                userId,
                "AUTH_LOGOUT",
                "RefreshToken",
                existing.Id,
                ct: ct
            );
        }

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<ForgotPasswordResponse>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken ct = default
    )
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (user is null)
        {
            return ServiceResult<ForgotPasswordResponse>.Ok(
                new ForgotPasswordResponse { ResetToken = null, ExpiresAt = null }
            );
        }

        var rawToken = _tokenService.GenerateRefreshToken();
        var tokenHash = _tokenService.HashRefreshToken(rawToken);
        var expiresAt = DateTimeOffset.UtcNow.Add(PasswordResetTtl);

        _db.PasswordResetTokens.Add(
            new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt,
            }
        );

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync(user.Id, "AUTH_FORGOT_PASSWORD", "User", user.Id, ct: ct);

        return ServiceResult<ForgotPasswordResponse>.Ok(
            new ForgotPasswordResponse { ResetToken = rawToken, ExpiresAt = expiresAt }
        );
    }

    private async Task<string> CreateEmailVerificationTokenAsync(
        User user,
        CancellationToken ct
    )
    {
        var rawToken = _tokenService.GenerateRefreshToken();
        var tokenHash = _tokenService.HashRefreshToken(rawToken);
        var expiresAt = DateTimeOffset.UtcNow.Add(EmailVerificationTtl);

        _db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
        });

        await _db.SaveChangesAsync(ct);

        return rawToken;
    }

    public async Task<ServiceResult<bool>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken ct = default
    )
    {
        var tokenHash = _tokenService.HashRefreshToken(request.Token);
        var token = await _db.PasswordResetTokens.FirstOrDefaultAsync(
            x => x.TokenHash == tokenHash,
            ct
        );
        if (token is null || token.UsedAt is not null || token.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return ServiceResult<bool>.Fail("invalid_reset_token");
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == token.UserId, ct);
        if (user is null)
        {
            return ServiceResult<bool>.Fail("user_not_found");
        }

        user.PasswordHash = _passwordHasher.Hash(request.Password);
        token.UsedAt = DateTimeOffset.UtcNow;

        var refreshTokens = await _db
            .RefreshTokens.Where(x => x.UserId == user.Id && x.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var refresh in refreshTokens)
        {
            refresh.RevokedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync(user.Id, "AUTH_RESET_PASSWORD", "User", user.Id, ct: ct);

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken ct = default
    )
    {
        var tokenHash = _tokenService.HashRefreshToken(request.Token);
        var token = await _db.EmailVerificationTokens.FirstOrDefaultAsync(
            x => x.TokenHash == tokenHash,
            ct
        );
        if (token is null || token.UsedAt is not null || token.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return ServiceResult<bool>.Fail("invalid_verify_token");
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == token.UserId, ct);
        if (user is null)
        {
            return ServiceResult<bool>.Fail("user_not_found");
        }

        if (user.EmailVerifiedAt is null)
        {
            user.EmailVerifiedAt = DateTimeOffset.UtcNow;
        }

        token.UsedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(user.Id, "AUTH_VERIFY_EMAIL", "User", user.Id, ct: ct);

        return ServiceResult<bool>.Ok(true);
    }

    private async Task<Role> EnsureRoleAsync(RoleName roleName, CancellationToken ct)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct);
        if (role is not null)
            return role;

        role = new Role { Name = roleName };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);
        return role;
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var roles = await _db
            .UserRoles.Where(ur => ur.UserId == user.Id)
            .Join(_db.Roles, ur => ur.RoleName, r => r.Name, (ur, r) => r.Name)
            .ToListAsync(ct);

        if (roles.Count == 0)
        {
            var userRole = await EnsureRoleAsync(RoleName.USER, ct);
            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleName = userRole.Name });
            await _db.SaveChangesAsync(ct);
            roles.Add(userRole.Name);
        }

        var subject = new TokenSubject(user.Id, user.Email, user.Username, user.Name);

        var roleStrings = roles.Select(r => r.ToString()).ToArray();

        var accessToken = _tokenService.GenerateAccessToken(subject, roleStrings);
        var refreshRaw = _tokenService.GenerateRefreshToken();
        var refreshHash = _tokenService.HashRefreshToken(refreshRaw);

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenDays),
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        var personal = await _db
            .UserPersonalData.Include(p => p.Address)
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        var address = personal?.Address;

        var personalView = personal is null
            ? null
            : new UserPersonalDataView
            {
                Cpf = personal.Cpf,
                PhoneNumber = personal.PhoneNumber,
                Address = address is null
                    ? null
                    : new UserAddressView
                    {
                        ZipCode = address.ZipCode,
                        Street = address.Street,
                        Neighborhood = address.Neighborhood,
                        Number = address.Number,
                        Complement = address.Complement,
                        City = address.City,
                        State = address.State,
                        Country = address.Country,
                    },
            };

        var userView = new UserView
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            Name = user.Name,
            AvatarUrl = user.AvatarUrl,
            Roles = roleStrings,
            PersonalData = personalView,
        };

        return new AuthResponse { AccessToken = accessToken, RefreshToken = refreshRaw, User = userView };
    }

    private static string ExtractUsernameFromEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
        {
            return email;
        }

        var localPart = email[..atIndex];
        var value = string.IsNullOrWhiteSpace(localPart) ? email : localPart;
        return RemoveDiacritics(value).ToLowerInvariant();
    }

    private static string NormalizeEmail(string email) =>
        RemoveDiacritics(email).Trim().ToLowerInvariant();

    private static string NormalizeName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        return RemoveDiacritics(trimmed);
    }

    private static string RemoveDiacritics(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        Span<char> buffer = stackalloc char[normalized.Length];
        var idx = 0;

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                buffer[idx++] = c;
            }
        }

        return new string(buffer[..idx]).Normalize(NormalizationForm.FormC);
    }
}

