using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pruduct.Business.Abstractions;
using Pruduct.Common.Enums;
using Pruduct.Data.Database.Contexts;
using Pruduct.Data.Models;

namespace Pruduct.Business.Services;

public class DatabaseSeeder : IDatabaseSeeder
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;

    public DatabaseSeeder(AppDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task SeedAsync(IConfiguration configuration, CancellationToken ct = default)
    {
        await _db.Database.MigrateAsync(ct);

        var roles = new[]
        {
            RoleName.USER,
            RoleName.ADMIN_L1,
            RoleName.ADMIN_L2,
            RoleName.ADMIN_L3,
        };
        foreach (var roleName in roles)
        {
            if (!await _db.Roles.AnyAsync(r => r.Name == roleName, ct))
            {
                _db.Roles.Add(new Role { Name = roleName });
            }
        }

        await _db.SaveChangesAsync(ct);

        var seedSection = configuration.GetSection("Seed:Admin");
        var adminEmail = seedSection.GetValue<string>("Email");
        var adminUsername = seedSection.GetValue<string>("Username") ?? "admin";
        var adminName = seedSection.GetValue<string>("Name") ?? "Admin";
        var adminPassword = seedSection.GetValue<string>("Password") ?? "ChangeMe123!";
        var adminCpf = seedSection.GetValue<string>("Cpf") ?? "00000000000";
        var adminAddress = seedSection.GetValue<string>("Address");
        var adminPhone = seedSection.GetValue<string>("PhoneNumber");

        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            await SeedDefaultUserAsync(configuration, ct);
            await SeedLedgerAsync(configuration, ct);
            return;
        }

        var normalizedEmail = NormalizeEmail(adminEmail);
        var normalizedName = NormalizeName(adminName);

        var admin = await _db
            .Users.Include(u => u.PersonalData)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (admin is null)
        {
            var username = string.IsNullOrWhiteSpace(adminUsername)
                ? ExtractUsernameFromEmail(normalizedEmail)
                : NormalizeUsername(adminUsername);
            username = await EnsureUniqueUsernameAsync(username, ct);

            admin = new User
            {
                Email = normalizedEmail,
                NormalizedEmail = normalizedEmail,
                Username = username,
                NormalizedUsername = username,
                Name = adminName,
                NormalizedName = normalizedName,
                PasswordHash = _hasher.Hash(adminPassword),
                Status = "ACTIVE",
                PersonalData = new UserPersonalData
                {
                    Cpf = adminCpf,
                    PhoneNumber = string.IsNullOrWhiteSpace(adminPhone) ? null : adminPhone,
                    Address = BuildAddress(seedSection, adminAddress),
                },
            };

            _db.Users.Add(admin);
            await _db.SaveChangesAsync(ct);
        }

        await EnsurePaymentMethodsAsync(admin, seedSection, ct);

        var adminRole = await _db.Roles.FirstAsync(r => r.Name == RoleName.ADMIN_L3, ct);
        if (
            !await _db.UserRoles.AnyAsync(
                ur => ur.UserId == admin.Id && ur.RoleName == adminRole.Name,
                ct
            )
        )
        {
            _db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleName = adminRole.Name });
            await _db.SaveChangesAsync(ct);
        }

        await SeedDefaultUserAsync(configuration, ct);
        await SeedLedgerAsync(configuration, ct);
    }

    private async Task SeedDefaultUserAsync(IConfiguration configuration, CancellationToken ct)
    {
        var seedSection = configuration.GetSection("Seed:User");
        var userEmail = seedSection.GetValue<string>("Email");
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return;
        }

        var userUsername = seedSection.GetValue<string>("Username") ?? "user";
        var userName = seedSection.GetValue<string>("Name") ?? "User";
        var userPassword = seedSection.GetValue<string>("Password") ?? "ChangeMe123!";
        var userCpf = seedSection.GetValue<string>("Cpf");
        var userAddress = seedSection.GetValue<string>("Address");
        var userPhone = seedSection.GetValue<string>("PhoneNumber");

        var normalizedEmail = NormalizeEmail(userEmail);
        var normalizedName = NormalizeName(userName);

        var user = await _db
            .Users.Include(u => u.PersonalData)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (user is null)
        {
            var username = string.IsNullOrWhiteSpace(userUsername)
                ? ExtractUsernameFromEmail(normalizedEmail)
                : NormalizeUsername(userUsername);
            username = await EnsureUniqueUsernameAsync(username, ct);

            user = new User
            {
                Email = normalizedEmail,
                NormalizedEmail = normalizedEmail,
                Username = username,
                NormalizedUsername = username,
                Name = userName,
                NormalizedName = normalizedName,
                PasswordHash = _hasher.Hash(userPassword),
                Status = "ACTIVE",
                PersonalData = null,
            };

            if (!string.IsNullOrWhiteSpace(userCpf))
            {
                user.PersonalData = new UserPersonalData
                {
                    Cpf = userCpf,
                    PhoneNumber = string.IsNullOrWhiteSpace(userPhone) ? null : userPhone,
                    Address = BuildAddress(seedSection, userAddress),
                };
            }

            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
        }

        await EnsurePaymentMethodsAsync(user, seedSection, ct);

        var userRole = await _db.Roles.FirstAsync(r => r.Name == RoleName.USER, ct);
        if (
            !await _db.UserRoles.AnyAsync(
                ur => ur.UserId == user.Id && ur.RoleName == userRole.Name,
                ct
            )
        )
        {
            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleName = userRole.Name });
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task SeedLedgerAsync(IConfiguration configuration, CancellationToken ct)
    {
        var ledgerSection = configuration.GetSection("Seed:Ledger");
        var enabled = ledgerSection.GetValue<bool?>("Enabled");
        if (enabled is false)
        {
            return;
        }

        var userEmail =
            ledgerSection.GetValue<string>("UserEmail")
            ?? configuration.GetSection("Seed:User").GetValue<string>("Email");
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return;
        }

        var normalizedEmail = NormalizeEmail(userEmail);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (user is null)
        {
            return;
        }

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.UserId == user.Id, ct);
        if (account is null)
        {
            account = new Account { UserId = user.Id, Currency = "BRL" };
            _db.Accounts.Add(account);
            await _db.SaveChangesAsync(ct);
        }

        var paymentKey = $"seed-payment-{user.Id}";
        var withdrawalKey = $"seed-withdrawal-{user.Id}";

        var payment = await _db.PaymentIntents.FirstOrDefaultAsync(
            x => x.IdempotencyKey == paymentKey,
            ct
        );
        if (payment is null)
        {
            payment = new PaymentIntent
            {
                UserId = user.Id,
                Provider = "SEED",
                Amount = 150_000,
                Currency = account.Currency,
                Status = PaymentIntentStatus.APPROVED,
                IdempotencyKey = paymentKey,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            };

            _db.PaymentIntents.Add(payment);
            await _db.SaveChangesAsync(ct);
        }

        var withdrawal = await _db.Withdrawals.FirstOrDefaultAsync(
            x => x.IdempotencyKey == withdrawalKey,
            ct
        );
        if (withdrawal is null)
        {
            withdrawal = new Withdrawal
            {
                UserId = user.Id,
                Amount = 20_000,
                Currency = account.Currency,
                Status = WithdrawalStatus.REQUESTED,
                IdempotencyKey = withdrawalKey,
                Notes = "Seed withdrawal",
            };

            _db.Withdrawals.Add(withdrawal);
            await _db.SaveChangesAsync(ct);
        }

        var baseTime = DateTimeOffset.UtcNow.AddDays(-5);
        var entries = new[]
        {
            new SeedLedgerEntry(
                $"seed-ledger-deposit-{user.Id}",
                LedgerEntryType.DEPOSIT_GATEWAY,
                150_000,
                "PaymentIntent",
                payment.Id,
                baseTime.AddHours(1)
            ),
            new SeedLedgerEntry(
                $"seed-ledger-buy-{user.Id}",
                LedgerEntryType.BET_BUY,
                -50_000,
                "Order",
                null,
                baseTime.AddHours(8)
            ),
            new SeedLedgerEntry(
                $"seed-ledger-payout-{user.Id}",
                LedgerEntryType.PAYOUT,
                80_000,
                "Market",
                null,
                baseTime.AddHours(16)
            ),
            new SeedLedgerEntry(
                $"seed-ledger-fee-{user.Id}",
                LedgerEntryType.FEE,
                -1_000,
                "Fee",
                null,
                baseTime.AddHours(20)
            ),
            new SeedLedgerEntry(
                $"seed-ledger-withdraw-{user.Id}",
                LedgerEntryType.WITHDRAW_REQUEST,
                -20_000,
                "Withdrawal",
                withdrawal.Id,
                baseTime.AddHours(28)
            ),
        };

        var existingKeys = await _db
            .LedgerEntries.Where(le => le.AccountId == account.Id && le.IdempotencyKey != null)
            .Select(le => le.IdempotencyKey!)
            .ToListAsync(ct);

        var toAdd = new List<LedgerEntry>();
        foreach (var entry in entries)
        {
            if (existingKeys.Contains(entry.IdempotencyKey))
            {
                continue;
            }

            toAdd.Add(
                new LedgerEntry
                {
                    AccountId = account.Id,
                    Type = entry.Type,
                    Amount = entry.Amount,
                    ReferenceType = entry.ReferenceType,
                    ReferenceId = entry.ReferenceId,
                    IdempotencyKey = entry.IdempotencyKey,
                    CreatedAt = entry.CreatedAt,
                    UpdatedAt = entry.CreatedAt,
                }
            );
        }

        if (toAdd.Count > 0)
        {
            _db.LedgerEntries.AddRange(toAdd);
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<string> EnsureUniqueUsernameAsync(string username, CancellationToken ct)
    {
        var candidate = username;
        var suffix = 1;
        while (await _db.Users.AnyAsync(u => u.NormalizedUsername == candidate, ct))
        {
            candidate = $"{username}{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string ExtractUsernameFromEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
        {
            return NormalizeUsername(email);
        }

        var localPart = email[..atIndex];
        var value = string.IsNullOrWhiteSpace(localPart) ? email : localPart;
        return NormalizeUsername(value);
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

    private static UserAddress? BuildAddress(IConfiguration section, string? fallbackStreet)
    {
        var addressSection = section.GetSection("Address");
        var zipCode = addressSection.GetValue<string>("ZipCode");
        var street = addressSection.GetValue<string>("Street");
        var neighborhood = addressSection.GetValue<string>("Neighborhood");
        var number = addressSection.GetValue<string>("Number");
        var complement = addressSection.GetValue<string>("Complement");
        var city = addressSection.GetValue<string>("City");
        var state = addressSection.GetValue<string>("State");
        var country = addressSection.GetValue<string>("Country");

        if (string.IsNullOrWhiteSpace(street))
        {
            street = fallbackStreet;
        }

        var hasAny =
            !string.IsNullOrWhiteSpace(zipCode)
            || !string.IsNullOrWhiteSpace(street)
            || !string.IsNullOrWhiteSpace(city)
            || !string.IsNullOrWhiteSpace(state);

        if (!hasAny)
        {
            return null;
        }

        return new UserAddress
        {
            ZipCode = string.IsNullOrWhiteSpace(zipCode) ? "00000000" : zipCode,
            Street = string.IsNullOrWhiteSpace(street) ? "N/A" : street,
            Neighborhood = string.IsNullOrWhiteSpace(neighborhood) ? null : neighborhood,
            Number = string.IsNullOrWhiteSpace(number) ? null : number,
            Complement = string.IsNullOrWhiteSpace(complement) ? null : complement,
            City = string.IsNullOrWhiteSpace(city) ? "N/A" : city,
            State = string.IsNullOrWhiteSpace(state) ? "NA" : state,
            Country = string.IsNullOrWhiteSpace(country) ? "BR" : country,
        };
    }

    private async Task EnsurePaymentMethodsAsync(
        User user,
        IConfiguration section,
        CancellationToken ct
    )
    {
        var methods = BuildPaymentMethods(section, user.Id);
        if (methods.Count == 0)
        {
            return;
        }

        var existing = await _db.PaymentMethods
            .Where(x => x.UserId == user.Id)
            .ToListAsync(ct);

        foreach (var method in methods)
        {
            if (existing.Any(x => IsSamePaymentMethod(x, method)))
            {
                continue;
            }

            _db.PaymentMethods.Add(method);
        }

        if (methods.Any(x => x.IsDefault) && existing.All(x => !x.IsDefault))
        {
            foreach (var item in existing)
            {
                item.IsDefault = false;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private static List<PaymentMethod> BuildPaymentMethods(IConfiguration section, Guid userId)
    {
        var bankSection = section.GetSection("BankAccount");
        var bankCode = bankSection.GetValue<string>("BankCode");
        var bankName = bankSection.GetValue<string>("BankName");
        var agency = bankSection.GetValue<string>("Agency");
        var accountNumber = bankSection.GetValue<string>("AccountNumber");
        var accountDigit = bankSection.GetValue<string>("AccountDigit");
        var accountType = bankSection.GetValue<string>("AccountType");
        var pixKey = bankSection.GetValue<string>("PixKey");

        var methods = new List<PaymentMethod>();

        if (!string.IsNullOrWhiteSpace(pixKey))
        {
            methods.Add(new PaymentMethod
            {
                UserId = userId,
                Type = PaymentMethodType.PIX,
                PixKey = pixKey,
                IsDefault = true,
            });
        }

        if (
            !string.IsNullOrWhiteSpace(bankCode)
            && !string.IsNullOrWhiteSpace(agency)
            && !string.IsNullOrWhiteSpace(accountNumber)
        )
        {
            methods.Add(new PaymentMethod
            {
                UserId = userId,
                Type = PaymentMethodType.BANK_ACCOUNT,
                BankCode = bankCode,
                BankName = bankName,
                Agency = agency,
                AccountNumber = accountNumber,
                AccountDigit = accountDigit,
                AccountType = accountType,
                IsDefault = methods.Count == 0,
            });
        }

        return methods;
    }

    private static bool IsSamePaymentMethod(PaymentMethod existing, PaymentMethod candidate)
    {
        if (existing.Type != candidate.Type)
        {
            return false;
        }

        return existing.Type switch
        {
            PaymentMethodType.PIX => string.Equals(existing.PixKey, candidate.PixKey),
            PaymentMethodType.CARD =>
                string.Equals(existing.CardLast4, candidate.CardLast4)
                && string.Equals(existing.CardBrand, candidate.CardBrand)
                && existing.CardExpMonth == candidate.CardExpMonth
                && existing.CardExpYear == candidate.CardExpYear,
            PaymentMethodType.BANK_ACCOUNT =>
                string.Equals(existing.BankCode, candidate.BankCode)
                && string.Equals(existing.Agency, candidate.Agency)
                && string.Equals(existing.AccountNumber, candidate.AccountNumber)
                && string.Equals(existing.AccountDigit, candidate.AccountDigit),
            _ => false,
        };
    }

    private sealed record SeedLedgerEntry(
        string IdempotencyKey,
        LedgerEntryType Type,
        long Amount,
        string ReferenceType,
        Guid? ReferenceId,
        DateTimeOffset CreatedAt
    );

    private static string RemoveDiacritics(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
