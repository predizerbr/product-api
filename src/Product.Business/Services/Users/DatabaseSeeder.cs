using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using Product.Business.Interfaces.Auth;
using Product.Business.Interfaces.Users;
using Product.Common.Enums;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Users;
using Product.Data.Models.Users.PaymentsMethods;
using Product.Data.Models.Wallet;

namespace Product.Business.Services.Users;

public class DatabaseSeeder : IDatabaseSeeder
{
    private readonly IDbMigrationRepository _migrationRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPaymentMethodRepository _paymentMethodRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly IPasswordHasher _hasher;

    public DatabaseSeeder(
        IDbMigrationRepository migrationRepository,
        IRoleRepository roleRepository,
        IUserRepository userRepository,
        IPaymentMethodRepository paymentMethodRepository,
        IWalletRepository walletRepository,
        IPasswordHasher hasher
    )
    {
        _migrationRepository = migrationRepository;
        _roleRepository = roleRepository;
        _userRepository = userRepository;
        _paymentMethodRepository = paymentMethodRepository;
        _walletRepository = walletRepository;
        _hasher = hasher;
    }

    public async Task SeedAsync(IConfiguration configuration, CancellationToken ct = default)
    {
        await _migrationRepository.MigrateAsync(ct);

        var roles = new[]
        {
            RoleName.USER,
            RoleName.ADMIN_L1,
            RoleName.ADMIN_L2,
            RoleName.ADMIN_L3,
        };
        foreach (var roleName in roles)
        {
            var roleValue = roleName.ToString();
            var normalizedRole = NormalizeRoleName(roleValue);
            if (!await _roleRepository.RoleExistsAsync(roleValue, ct))
            {
                await _roleRepository.AddRoleAsync(
                    new Role
                    {
                        Id = Guid.NewGuid(),
                        Name = roleValue,
                        NormalizedName = normalizedRole,
                        ConcurrencyStamp = Guid.NewGuid().ToString(),
                    },
                    ct
                );
            }
        }

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

        var admin = await _userRepository.GetUserWithPersonalDataByEmailAsync(normalizedEmail, ct);
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
                UserName = username,
                NormalizedUserName = username,
                Name = adminName,
                NormalizedName = normalizedName,
                PasswordHash = _hasher.Hash(adminPassword),
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                EmailConfirmed = true,
                EmailVerifiedAt = DateTimeOffset.UtcNow,
                Status = "ACTIVE",
                PersonalData = new UserPersonalData
                {
                    Cpf = adminCpf,
                    PhoneNumber = string.IsNullOrWhiteSpace(adminPhone) ? null : adminPhone,
                    Address = BuildAddress(seedSection, adminAddress),
                },
            };

            await _userRepository.AddUserAsync(admin, ct);
        }

        await EnsurePaymentMethodsAsync(admin, seedSection, ct);

        var adminRole = await _roleRepository.GetRoleByNameAsync(RoleName.ADMIN_L3.ToString(), ct);
        if (adminRole is null)
        {
            throw new InvalidOperationException("role_not_found");
        }
        if (!await _roleRepository.UserRoleExistsAsync(admin.Id, adminRole.Id, ct))
        {
            await _roleRepository.AddUserRoleAsync(admin.Id, adminRole.Id, ct);
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

        var user = await _userRepository.GetUserWithPersonalDataByEmailAsync(normalizedEmail, ct);
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
                UserName = username,
                NormalizedUserName = username,
                Name = userName,
                NormalizedName = normalizedName,
                PasswordHash = _hasher.Hash(userPassword),
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                EmailConfirmed = true,
                EmailVerifiedAt = DateTimeOffset.UtcNow,
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

            await _userRepository.AddUserAsync(user, ct);
        }

        await EnsurePaymentMethodsAsync(user, seedSection, ct);

        var userRole = await _roleRepository.GetRoleByNameAsync(RoleName.USER.ToString(), ct);
        if (userRole is null)
        {
            throw new InvalidOperationException("role_not_found");
        }
        if (!await _roleRepository.UserRoleExistsAsync(user.Id, userRole.Id, ct))
        {
            await _roleRepository.AddUserRoleAsync(user.Id, userRole.Id, ct);
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
        var user = await _userRepository.GetUserByEmailAsync(normalizedEmail, ct);
        if (user is null)
        {
            return;
        }

        var accounts = await _walletRepository.EnsureAccountsAsync(user.Id, "BRL", ct);
        var account = accounts[0];

        var paymentKey = $"seed-payment-{user.Id}";
        var withdrawalKey = $"seed-withdrawal-{user.Id}";

        var payment = await _walletRepository.GetPaymentIntentByIdempotencyAsync(
            user.Id,
            paymentKey,
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

            await _walletRepository.AddPaymentIntentAsync(payment, ct);
        }

        var withdrawal = await _walletRepository.GetWithdrawalByIdempotencyAsync(
            user.Id,
            withdrawalKey,
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

            await _walletRepository.AddWithdrawalAsync(withdrawal, ct);
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

        var existingKeys = await _walletRepository.GetLedgerEntryIdempotencyKeysAsync(
            account.Id,
            ct
        );

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
            await _walletRepository.AddLedgerEntriesAsync(toAdd, ct);
        }
    }

    private async Task<string> EnsureUniqueUsernameAsync(string username, CancellationToken ct)
    {
        var candidate = username;
        var suffix = 1;
        while (await _userRepository.UserNameExistsAsync(candidate, ct))
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

    private static string NormalizeRoleName(string roleName) =>
        RemoveDiacritics(roleName).Trim().ToLowerInvariant();

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
        var (cardsToAdd, banksToAdd, pixToAdd) = BuildPaymentMethods(section, user.Id);
        if (cardsToAdd.Count == 0 && banksToAdd.Count == 0 && pixToAdd.Count == 0)
        {
            return;
        }

        var (existingCards, existingBanks, existingPix) = await _paymentMethodRepository.GetByUserAsync(user.Id, ct);

        var addedAny = false;

        foreach (var card in cardsToAdd)
        {
            if (existingCards.Any(x => IsSameUserCard(x, card)))
            {
                continue;
            }

            await _paymentMethodRepository.AddUserCardAsync(card, ct);
            addedAny = true;
        }

        foreach (var bank in banksToAdd)
        {
            if (existingBanks.Any(x => IsSameUserBank(x, bank)))
            {
                continue;
            }

            await _paymentMethodRepository.AddUserBankAccountAsync(bank, ct);
            addedAny = true;
        }

        foreach (var pix in pixToAdd)
        {
            if (existingPix.Any(x => IsSameUserPix(x, pix)))
            {
                continue;
            }

            await _paymentMethodRepository.AddUserPixKeyAsync(pix, ct);
            addedAny = true;
        }

        if (addedAny)
        {
            await _paymentMethodRepository.SaveChangesAsync(ct);
        }
    }

    private static (List<UserCard> Cards, List<UserBankAccount> Banks, List<UserPixKey> Pix) BuildPaymentMethods(
        IConfiguration section,
        Guid userId
    )
    {
        var bankSection = section.GetSection("BankAccount");
        var bankCode = bankSection.GetValue<string>("BankCode");
        var bankName = bankSection.GetValue<string>("BankName");
        var agency = bankSection.GetValue<string>("Agency");
        var accountNumber = bankSection.GetValue<string>("AccountNumber");
        var accountDigit = bankSection.GetValue<string>("AccountDigit");
        var accountType = bankSection.GetValue<string>("AccountType");
        var pixKey = bankSection.GetValue<string>("PixKey");

        var cards = new List<UserCard>();
        var banks = new List<UserBankAccount>();
        var pix = new List<UserPixKey>();

        if (!string.IsNullOrWhiteSpace(pixKey))
        {
            pix.Add(
                new UserPixKey
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PixKey = pixKey,
                    IsDefault = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        }

        if (
            !string.IsNullOrWhiteSpace(bankCode)
            && !string.IsNullOrWhiteSpace(agency)
            && !string.IsNullOrWhiteSpace(accountNumber)
        )
        {
            banks.Add(
                new UserBankAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    BankCode = bankCode,
                    BankName = bankName,
                    Agency = agency,
                    AccountNumber = accountNumber,
                    AccountDigit = accountDigit,
                    AccountType = accountType,
                    IsDefault = pix.Count == 0 && banks.Count == 0,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        }

        return (cards, banks, pix);
    }

    private static bool IsSameUserCard(UserCard existing, UserCard candidate) =>
        string.Equals(existing.CardLast4, candidate.CardLast4)
        && string.Equals(existing.CardBrand, candidate.CardBrand)
        && existing.CardExpMonth == candidate.CardExpMonth
        && existing.CardExpYear == candidate.CardExpYear;

    private static bool IsSameUserBank(UserBankAccount existing, UserBankAccount candidate) =>
        string.Equals(existing.BankCode, candidate.BankCode)
        && string.Equals(existing.Agency, candidate.Agency)
        && string.Equals(existing.AccountNumber, candidate.AccountNumber)
        && string.Equals(existing.AccountDigit, candidate.AccountDigit);

    private static bool IsSameUserPix(UserPixKey existing, UserPixKey candidate) =>
        string.Equals(existing.PixKey, candidate.PixKey);

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
