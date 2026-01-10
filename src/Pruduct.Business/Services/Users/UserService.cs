using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pruduct.Business.Interfaces.Audit;
using Pruduct.Business.Interfaces.Auth;
using Pruduct.Business.Interfaces.Results;
using Pruduct.Business.Interfaces.Users;
using Pruduct.Contracts.Users;
using Pruduct.Data.Database.Contexts;
using Pruduct.Data.Models.Users;

namespace Pruduct.Business.Services.Users;

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditService _auditService;

    public UserService(AppDbContext db, IPasswordHasher passwordHasher, IAuditService auditService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _auditService = auditService;
    }

    public async Task<ServiceResult<UserView>> GetMeAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var user = await _db
            .Users.Include(u => u.PersonalData)
            .ThenInclude(pd => pd!.Address)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return ServiceResult<UserView>.Fail("user_not_found");
        }

        var view = await BuildUserViewAsync(user, ct);
        return ServiceResult<UserView>.Ok(view);
    }

    public async Task<ServiceResult<UserView>> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken ct = default
    )
    {
        var user = await _db
            .Users.Include(u => u.PersonalData)
            .ThenInclude(pd => pd!.Address)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return ServiceResult<UserView>.Fail("user_not_found");
        }

        if (request.Email is not null)
        {
            var normalizedEmail = NormalizeEmail(request.Email);
            var emailTaken = await _db.Users.AnyAsync(
                u => u.Id != userId && u.NormalizedEmail == normalizedEmail,
                ct
            );
            if (emailTaken)
            {
                return ServiceResult<UserView>.Fail("email_taken");
            }

            user.Email = normalizedEmail;
            user.NormalizedEmail = normalizedEmail;
        }

        if (request.Username is not null)
        {
            var normalizedUsername = NormalizeUsername(request.Username);
            var usernameTaken = await _db.Users.AnyAsync(
                u => u.Id != userId && u.NormalizedUserName == normalizedUsername,
                ct
            );
            if (usernameTaken)
            {
                return ServiceResult<UserView>.Fail("username_taken");
            }

            user.UserName = normalizedUsername;
            user.NormalizedUserName = normalizedUsername;
        }

        if (request.Name is not null)
        {
            user.Name = request.Name;
            user.NormalizedName = NormalizeName(request.Name);
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = _passwordHasher.Hash(request.Password);
            user.SecurityStamp = Guid.NewGuid().ToString();
        }

        try
        {
            await HandleProfilePersonalDataUpdateAsync(user, request, ct);
        }
        catch (InvalidOperationException ex)
        {
            return ServiceResult<UserView>.Fail(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(user.Id, "USER_UPDATE", "User", user.Id, ct: ct);
        if (request.Cpf is not null || request.PhoneNumber is not null)
        {
            await _auditService.LogAsync(
                user.Id,
                "USER_PERSONAL_UPDATE",
                "UserPersonalData",
                user.PersonalData?.Id,
                ct: ct
            );
        }

        var view = await BuildUserViewAsync(user, ct);
        return ServiceResult<UserView>.Ok(view);
    }

    public async Task<ServiceResult<UserView>> UpdateAddressAsync(
        Guid userId,
        UpdateAddressRequest request,
        CancellationToken ct = default
    )
    {
        var user = await _db
            .Users.Include(u => u.PersonalData)
            .ThenInclude(pd => pd!.Address)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return ServiceResult<UserView>.Fail("user_not_found");
        }

        if (user.PersonalData is null)
        {
            if (
                string.IsNullOrWhiteSpace(request.ZipCode)
                || string.IsNullOrWhiteSpace(request.Street)
                || string.IsNullOrWhiteSpace(request.City)
                || string.IsNullOrWhiteSpace(request.State)
            )
            {
                return ServiceResult<UserView>.Fail("address_required");
            }

            user.PersonalData = new UserPersonalData
            {
                UserId = user.Id,
                Cpf = null,
                PhoneNumber = null,
                Address = new UserAddress
                {
                    ZipCode = request.ZipCode!,
                    Street = request.Street!,
                    Neighborhood = request.Neighborhood,
                    Number = request.Number,
                    Complement = request.Complement,
                    City = request.City!,
                    State = request.State!,
                    Country = string.IsNullOrWhiteSpace(request.Country) ? "BR" : request.Country!,
                },
            };
        }
        else if (user.PersonalData.Address is null)
        {
            if (
                string.IsNullOrWhiteSpace(request.ZipCode)
                || string.IsNullOrWhiteSpace(request.Street)
                || string.IsNullOrWhiteSpace(request.City)
                || string.IsNullOrWhiteSpace(request.State)
            )
            {
                return ServiceResult<UserView>.Fail("address_required");
            }

            user.PersonalData.Address = new UserAddress
            {
                ZipCode = request.ZipCode!,
                Street = request.Street!,
                Neighborhood = request.Neighborhood,
                Number = request.Number,
                Complement = request.Complement,
                City = request.City!,
                State = request.State!,
                Country = string.IsNullOrWhiteSpace(request.Country) ? "BR" : request.Country!,
            };
        }
        else
        {
            if (request.ZipCode is not null)
            {
                user.PersonalData.Address.ZipCode = request.ZipCode;
            }

            if (request.Street is not null)
            {
                user.PersonalData.Address.Street = request.Street;
            }

            if (request.Neighborhood is not null)
            {
                user.PersonalData.Address.Neighborhood = request.Neighborhood;
            }

            if (request.Number is not null)
            {
                user.PersonalData.Address.Number = request.Number;
            }

            if (request.Complement is not null)
            {
                user.PersonalData.Address.Complement = request.Complement;
            }

            if (request.City is not null)
            {
                user.PersonalData.Address.City = request.City;
            }

            if (request.State is not null)
            {
                user.PersonalData.Address.State = request.State;
            }

            if (request.Country is not null)
            {
                user.PersonalData.Address.Country = request.Country;
            }
        }

        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            user.Id,
            "USER_ADDRESS_UPDATE",
            "UserAddress",
            user.PersonalData.Address?.Id,
            ct: ct
        );

        var view = await BuildUserViewAsync(user, ct);
        return ServiceResult<UserView>.Ok(view);
    }

    public async Task<ServiceResult<UserView>> UpdateAvatarAsync(
        Guid userId,
        UpdateAvatarRequest request,
        CancellationToken ct = default
    )
    {
        var user = await _db
            .Users.Include(u => u.PersonalData)
            .ThenInclude(pd => pd!.Address)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return ServiceResult<UserView>.Fail("user_not_found");
        }

        user.AvatarUrl = request.AvatarUrl;
        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(user.Id, "USER_AVATAR_UPDATE", "User", user.Id, ct: ct);

        var view = await BuildUserViewAsync(user, ct);
        return ServiceResult<UserView>.Ok(view);
    }

    public async Task<ServiceResult<IReadOnlyCollection<UserSessionResponse>>> GetSessionsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var sessions = await _db
            .RefreshTokens.Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new UserSessionResponse
            {
                Id = x.Id,
                DeviceInfo = x.DeviceInfo,
                CreatedAt = x.CreatedAt,
                ExpiresAt = x.ExpiresAt,
                RevokedAt = x.RevokedAt,
                IsActive = x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow,
            })
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyCollection<UserSessionResponse>>.Ok(sessions);
    }

    public async Task<ServiceResult<bool>> RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken ct = default
    )
    {
        var session = await _db.RefreshTokens.FirstOrDefaultAsync(
            x => x.Id == sessionId && x.UserId == userId,
            ct
        );
        if (session is null)
        {
            return ServiceResult<bool>.Fail("session_not_found");
        }

        if (session.RevokedAt is null)
        {
            session.RevokedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        await _auditService.LogAsync(userId, "SESSION_REVOKE", "RefreshToken", session.Id, ct: ct);

        return ServiceResult<bool>.Ok(true);
    }

    private async Task HandleProfilePersonalDataUpdateAsync(
        User user,
        UpdateProfileRequest request,
        CancellationToken ct
    )
    {
        if (request.Cpf is null && request.PhoneNumber is null)
        {
            return;
        }

        if (user.PersonalData is null)
        {
            if (!string.IsNullOrWhiteSpace(request.Cpf))
            {
                var cpfTaken = await _db.UserPersonalData.AnyAsync(
                    x => x.UserId != user.Id && x.Cpf == request.Cpf,
                    ct
                );
                if (cpfTaken)
                {
                    throw new InvalidOperationException("cpf_taken");
                }
            }

            user.PersonalData = new UserPersonalData
            {
                UserId = user.Id,
                Cpf = string.IsNullOrWhiteSpace(request.Cpf) ? null : request.Cpf,
                PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
                    ? null
                    : request.PhoneNumber,
            };

            return;
        }

        if (!string.IsNullOrWhiteSpace(request.Cpf))
        {
            var cpfTaken = await _db.UserPersonalData.AnyAsync(
                x => x.UserId != user.Id && x.Cpf == request.Cpf,
                ct
            );
            if (cpfTaken)
            {
                throw new InvalidOperationException("cpf_taken");
            }

            user.PersonalData.Cpf = request.Cpf!;
        }

        if (request.PhoneNumber is not null)
        {
            user.PersonalData.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
                ? null
                : request.PhoneNumber;
        }
    }

    private async Task<UserView> BuildUserViewAsync(User user, CancellationToken ct)
    {
        var roles = await _db
            .UserRoles.Where(ur => ur.UserId == user.Id)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name ?? string.Empty)
            .ToArrayAsync(ct);

        var personal = user.PersonalData;
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

        return new UserView
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            Username = user.UserName ?? string.Empty,
            Name = user.Name,
            AvatarUrl = user.AvatarUrl,
            Roles = roles,
            PersonalData = personalView,
        };
    }

    private static string NormalizeEmail(string email) =>
        RemoveDiacritics(email).Trim().ToLowerInvariant();

    private static string NormalizeName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        return RemoveDiacritics(trimmed);
    }

    private static string NormalizeUsername(string username) =>
        RemoveDiacritics(username).Trim().ToLowerInvariant();

    private static string RemoveDiacritics(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        Span<char> buffer = stackalloc char[normalized.Length];
        var idx = 0;

        foreach (var c in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                buffer[idx++] = c;
            }
        }

        return new string(buffer[..idx]).Normalize(System.Text.NormalizationForm.FormC);
    }
}
