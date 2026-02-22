using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Product.Business.Interfaces.Auth;
using Product.Business.Interfaces.Categories;
using Product.Business.Interfaces.Users;
using Product.Common.Enums;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Markets;
using Product.Data.Models.Users;
using Product.Data.Models.Users.PaymentsMethods;
using Product.Data.Models.Wallet;

namespace Product.Business.Services.Users;

public class DatabaseSeeder(
    IDbMigrationRepository migrationRepository,
    IUserRepository userRepository,
    IPaymentMethodRepository paymentMethodRepository,
    IWalletRepository walletRepository,
    IPasswordHasher hasher,
    ICategoryService categoryService,
    IRolePromotionService rolePromotionService,
    AppDbContext db
) : IDatabaseSeeder
{
    private readonly IDbMigrationRepository _migrationRepository = migrationRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IPaymentMethodRepository _paymentMethodRepository = paymentMethodRepository;
    private readonly IWalletRepository _walletRepository = walletRepository;
    private readonly IPasswordHasher _hasher = hasher;
    private readonly ICategoryService _categoryService = categoryService;
    private readonly IRolePromotionService _rolePromotionService = rolePromotionService;
    private readonly AppDbContext _db = db;

    private sealed record SeedAddress(
        string ZipCode,
        string Street,
        string Neighborhood,
        string Number,
        string Complement,
        string City,
        string State,
        string Country
    );

    private sealed record SeedBankAccount(
        string BankCode,
        string BankName,
        string Agency,
        string AccountNumber,
        string AccountDigit,
        string AccountType,
        string PixKey
    );

    private sealed record SeedUserData(
        string Email,
        string Username,
        string Name,
        string Password,
        string Cpf,
        string PhoneNumber,
        SeedAddress Address,
        SeedBankAccount BankAccount
    );

    public async Task SeedAsync(IConfiguration configuration, CancellationToken ct = default)
    {
        await _migrationRepository.MigrateAsync(ct);

        // Ensure default categories are seeded
        try
        {
            await _categoryService.EnsureDefaultCategoriesAsync(ct);
            await FixCategoryNamesAsync(ct);
        }
        catch
        {
            // swallow category seeding errors
        }

        var adminData = GetAdminSeed();

        var normalizedEmail = NormalizeEmail(adminData.Email);
        var normalizedName = NormalizeName(adminData.Name);

        var admin = await _userRepository.GetUserWithPersonalDataByEmailAsync(normalizedEmail, ct);
        if (admin is null)
        {
            var username = string.IsNullOrWhiteSpace(adminData.Username)
                ? ExtractUsernameFromEmail(normalizedEmail)
                : NormalizeUsername(adminData.Username);
            username = await EnsureUniqueUsernameAsync(username, ct);

            admin = new ApplicationUser
            {
                Email = normalizedEmail,
                NormalizedEmail = normalizedEmail,
                UserName = username,
                NormalizedUserName = username,
                Name = adminData.Name,
                PasswordHash = _hasher.Hash(adminData.Password),
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                EmailConfirmed = true,
                EmailVerifiedAt = DateTimeOffset.UtcNow,
                Status = "ACTIVE",
                PersonalData = new UserPersonalData
                {
                    Cpf = adminData.Cpf,
                    PhoneNumber = string.IsNullOrWhiteSpace(adminData.PhoneNumber)
                        ? null
                        : adminData.PhoneNumber,
                    Address = BuildAddressFromSeed(adminData.Address),
                },
            };

            await _userRepository.AddUserAsync(admin, ct);
        }

        await EnsurePaymentMethodsAsync(admin, adminData, ct);

        // Ensure admin has the role string set
        await _rolePromotionService.PromoteToRoleAsync(admin.Id, RoleName.ADMIN_L3.ToString(), ct);

        await SeedDefaultUserAsync(ct);
        await SeedLedgerAsync(ct);
        await SeedMarketsAsync(admin, ct);
    }

    private async Task SeedDefaultUserAsync(CancellationToken ct)
    {
        var userData = GetUserSeed();

        var normalizedEmail = NormalizeEmail(userData.Email);
        var normalizedName = NormalizeName(userData.Name);

        var user = await _userRepository.GetUserWithPersonalDataByEmailAsync(normalizedEmail, ct);
        if (user is null)
        {
            var username = string.IsNullOrWhiteSpace(userData.Username)
                ? ExtractUsernameFromEmail(normalizedEmail)
                : NormalizeUsername(userData.Username);
            username = await EnsureUniqueUsernameAsync(username, ct);

            user = new ApplicationUser
            {
                Email = normalizedEmail,
                NormalizedEmail = normalizedEmail,
                UserName = username,
                NormalizedUserName = username,
                Name = userData.Name,
                PasswordHash = _hasher.Hash(userData.Password),
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                EmailConfirmed = true,
                EmailVerifiedAt = DateTimeOffset.UtcNow,
                Status = "ACTIVE",
                PersonalData = new UserPersonalData
                {
                    Cpf = userData.Cpf,
                    PhoneNumber = string.IsNullOrWhiteSpace(userData.PhoneNumber)
                        ? null
                        : userData.PhoneNumber,
                    Address = BuildAddressFromSeed(userData.Address),
                },
            };

            await _userRepository.AddUserAsync(user, ct);
        }

        await EnsurePaymentMethodsAsync(user, userData, ct);

        // Ensure default user has role string set
        await _rolePromotionService.PromoteToRoleAsync(user.Id, RoleName.USER.ToString(), ct);
    }

    private async Task SeedLedgerAsync(CancellationToken ct)
    {
        var enabled = true;
        if (enabled is false)
        {
            return;
        }

        var userEmail = GetUserSeed().Email;
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
                LedgerEntryType.MARKET_BUY,
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

    private async Task SeedMarketsAsync(ApplicationUser admin, CancellationToken ct)
    {
        var seeds = GetMarketSeeds();
        if (seeds.Count == 0)
        {
            return;
        }

        foreach (var seed in seeds)
        {
            var exists = await _db.Markets.AnyAsync(m => m.Title == seed.Title, ct);
            if (exists)
            {
                continue;
            }

            var yesPrice = Math.Clamp(seed.Probability / 100m, 0.01m, 0.99m);
            var market = new Market
            {
                Id = Guid.NewGuid(),
                Title = seed.Title,
                Description = seed.Description,
                Category = NormalizeCategory(seed.Category),
                Tags = string.Join(",", seed.Tags),
                ClosingDate = seed.ClosingDate,
                ResolutionDate = seed.ResolutionDate,
                ResolutionSource = seed.ResolutionSource,
                YesPrice = yesPrice,
                NoPrice = 1m - yesPrice,
                Featured = seed.Featured,
                Status = "open",
                CreatedBy = admin.Id,
                CreatorEmail = admin.Email,
                VolumeTotal = 0m,
                Volume24h = 0m,
                Volatility24h = 0m,
                YesContracts = 0,
                NoContracts = 0,
                LowLiquidityWarning = false,
                ProbabilityBucket = seed.Probability.ToString(CultureInfo.InvariantCulture),
                SearchSnippet = seed.Title,
            };

            _db.Markets.Add(market);
            await _db.SaveChangesAsync(ct);
            await _categoryService.AssociateCategoryWithMarketAsync(market, ct);
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

    private static SeedUserData GetAdminSeed()
    {
        return new SeedUserData(
            "predizer@predizer.com",
            "predizer-admin",
            "Predizer Admin L3",
            "ChangeMe123!",
            "04205009011",
            "11996987654",
            new SeedAddress(
                "01311-200",
                "Avenida Paulista",
                "Bela Vista",
                string.Empty,
                string.Empty,
                "Sao Paulo",
                "SP",
                "BR"
            ),
            new SeedBankAccount(
                "001",
                "Banco do Brasil",
                "0001",
                "123456",
                "7",
                "CHECKING",
                "predizer@predizer.com"
            )
        );
    }

    private static SeedUserData GetUserSeed()
    {
        return new SeedUserData(
            "johndoe@predizer.com",
            "johndoe",
            "John Doe Smith",
            "ChangeMe123!",
            "16864772012",
            "21997123456",
            new SeedAddress(
                "22452330",
                "Rua João Paulo I",
                "Vidigal",
                "100",
                "Apto 12",
                "Rio de Janeiro",
                "RJ",
                "BR"
            ),
            new SeedBankAccount(
                "033",
                "Santander",
                "1234",
                "987654",
                "0",
                "CHECKING",
                "johndoe@predizer.com"
            )
        );
    }

    private static UserAddress BuildAddressFromSeed(SeedAddress s)
    {
        return new UserAddress
        {
            ZipCode = string.IsNullOrWhiteSpace(s.ZipCode) ? "00000000" : s.ZipCode,
            Street = string.IsNullOrWhiteSpace(s.Street) ? "N/A" : s.Street,
            Neighborhood = string.IsNullOrWhiteSpace(s.Neighborhood) ? null : s.Neighborhood,
            Number = string.IsNullOrWhiteSpace(s.Number) ? null : s.Number,
            Complement = string.IsNullOrWhiteSpace(s.Complement) ? null : s.Complement,
            City = string.IsNullOrWhiteSpace(s.City) ? "N/A" : s.City,
            State = string.IsNullOrWhiteSpace(s.State) ? "NA" : s.State,
            Country = string.IsNullOrWhiteSpace(s.Country) ? "BR" : s.Country,
        };
    }

    private async Task EnsurePaymentMethodsAsync(
        ApplicationUser user,
        SeedUserData seed,
        CancellationToken ct
    )
    {
        var (cardsToAdd, banksToAdd, pixToAdd) = BuildPaymentMethodsFromSeed(seed, user.Id);
        if (cardsToAdd.Count == 0 && banksToAdd.Count == 0 && pixToAdd.Count == 0)
        {
            return;
        }

        var (existingCards, existingBanks, existingPix) =
            await _paymentMethodRepository.GetByUserAsync(user.Id, ct);

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

    private static (
        List<UserCard> Cards,
        List<UserBankAccount> Banks,
        List<UserPixKey> Pix
    ) BuildPaymentMethodsFromSeed(SeedUserData seed, Guid userId)
    {
        var cards = new List<UserCard>();
        var banks = new List<UserBankAccount>();
        var pix = new List<UserPixKey>();

        var pixKey = seed.BankAccount.PixKey;
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
            !string.IsNullOrWhiteSpace(seed.BankAccount.BankCode)
            && !string.IsNullOrWhiteSpace(seed.BankAccount.Agency)
            && !string.IsNullOrWhiteSpace(seed.BankAccount.AccountNumber)
        )
        {
            banks.Add(
                new UserBankAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    BankCode = seed.BankAccount.BankCode,
                    BankName = seed.BankAccount.BankName,
                    Agency = seed.BankAccount.Agency,
                    AccountNumber = seed.BankAccount.AccountNumber,
                    AccountDigit = seed.BankAccount.AccountDigit,
                    AccountType = seed.BankAccount.AccountType,
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

    private sealed record SeedMarket(
        string Title,
        string Description,
        string Category,
        IReadOnlyList<string> Tags,
        decimal Probability,
        DateTimeOffset ClosingDate,
        DateTimeOffset ResolutionDate,
        string ResolutionSource,
        bool Featured
    );

    private static List<SeedMarket> GetMarketSeeds()
    {
        return new List<SeedMarket>
        {
            new(
                "Selic ficara acima de 10% em 2026?",
                "Resolve SIM se a taxa Selic (meta) vigente na data de resolucao estiver > 10,00%, conforme decisao do Copom.",
                "ECONOMIA",
                new[] { "selic", "copom", "juros" },
                52,
                DateTimeOffset.Parse("2026-02-01T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-02-10T20:00:00.000Z", CultureInfo.InvariantCulture),
                "Banco Central do Brasil (Copom)",
                true
            ),
            new(
                "IPCA 12 meses ficara abaixo de 4,5% em mar/2026?",
                "Resolve SIM se o IPCA acumulado em 12 meses divulgado pelo IBGE para mar/2026 for < 4,5%.",
                "ECONOMIA",
                new[] { "ipca", "inflacao", "ibge" },
                46,
                DateTimeOffset.Parse("2026-04-05T18:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-04-10T12:00:00.000Z", CultureInfo.InvariantCulture),
                "IBGE (IPCA)",
                false
            ),
            new(
                "Congresso aprovara uma reforma tributaria complementar ate 30/06/2026?",
                "Resolve SIM se o projeto/PLP definido no enunciado for aprovado ate 30/06/2026 (ver tramitacao oficial).",
                "POLITICA",
                new[] { "congresso", "reforma-tributaria", "camara", "senado" },
                38,
                DateTimeOffset.Parse("2026-06-25T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-07-01T12:00:00.000Z", CultureInfo.InvariantCulture),
                "Camara dos Deputados e Senado Federal (tramitacao oficial)",
                false
            ),
            new(
                "Um novo ministerio sera criado ou fundido ate 31/12/2026?",
                "Resolve SIM se houver criacao, fusao ou extincao de ministerio publicada em ato oficial ate 31/12/2026.",
                "POLITICA",
                new[] { "governo", "ministerios", "decreto" },
                22,
                DateTimeOffset.Parse("2026-12-10T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2027-01-05T12:00:00.000Z", CultureInfo.InvariantCulture),
                "Diario Oficial da Uniao (atos do Poder Executivo)",
                true
            ),
            new(
                "Brasil vencera ao menos 1 medalha de ouro no Mundial (modalidade escolhida) em 2026?",
                "Resolve SIM se o Brasil conquistar ao menos 1 ouro no Mundial especificado no enunciado, conforme resultados oficiais.",
                "ESPORTES",
                new[] { "mundial", "medalhas", "brasil" },
                28,
                DateTimeOffset.Parse("2026-08-01T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-09-01T12:00:00.000Z", CultureInfo.InvariantCulture),
                "Federacao/organizacao oficial do evento (resultados)",
                false
            ),
            new(
                "Flamengo terminara o Brasileirao 2026 no G4?",
                "Resolve SIM se o Flamengo terminar o Brasileirao 2026 entre os 4 primeiros colocados na classificacao final.",
                "ESPORTES",
                new[] { "brasileirao", "g4", "flamengo" },
                35,
                DateTimeOffset.Parse("2026-11-20T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-12-10T12:00:00.000Z", CultureInfo.InvariantCulture),
                "CBF (classificacao oficial do Brasileirao)",
                true
            ),
            new(
                "Um filme brasileiro sera indicado ao Oscar (categoria principal) em 2027?",
                "Resolve SIM se um filme brasileiro for indicado em Melhor Filme ou Melhor Filme Internacional na lista oficial de indicados.",
                "CULTURA",
                new[] { "oscar", "cinema", "brasil" },
                14,
                DateTimeOffset.Parse("2027-01-15T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2027-02-01T18:00:00.000Z", CultureInfo.InvariantCulture),
                "Academy Awards (lista oficial de indicados)",
                false
            ),
            new(
                "Uma turne internacional grande anunciara datas no Brasil ate 31/12/2026?",
                "Resolve SIM se uma turne internacional considerada 'grande' (definida no enunciado) anunciar oficialmente datas no Brasil ate 31/12/2026.",
                "CULTURA",
                new[] { "shows", "turne", "brasil" },
                50,
                DateTimeOffset.Parse("2026-12-10T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2027-01-05T12:00:00.000Z", CultureInfo.InvariantCulture),
                "Comunicado oficial do artista/produtora + imprensa confiavel",
                true
            ),
            new(
                "Bitcoin ficara acima de US$ 100.000 em 31/12/2026?",
                "Resolve SIM se o preco BTC/USD estiver >= 100.000 na data de resolucao, conforme fonte definida.",
                "CRIPTOMOEDAS",
                new[] { "btc", "bitcoin", "cripto" },
                33,
                DateTimeOffset.Parse("2026-12-20T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2027-01-02T12:00:00.000Z", CultureInfo.InvariantCulture),
                "CoinMarketCap (BTC/USD spot)",
                true
            ),
            new(
                "Ethereum ficara acima de US$ 6.000 em 31/12/2026?",
                "Resolve SIM se o preco ETH/USD estiver >= 6.000 na data de resolucao, conforme fonte definida.",
                "CRIPTOMOEDAS",
                new[] { "eth", "ethereum", "cripto" },
                27,
                DateTimeOffset.Parse("2026-12-20T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2027-01-02T12:00:00.000Z", CultureInfo.InvariantCulture),
                "CoinMarketCap (ETH/USD spot)",
                false
            ),
            new(
                "Uma onda de calor severa sera confirmada no Brasil ate 31/03/2026?",
                "Resolve SIM se um orgao oficial confirmar ocorrencia de onda de calor severa no Brasil ate 31/03/2026 (definicao oficial).",
                "CLIMA",
                new[] { "onda-de-calor", "clima", "brasil" },
                58,
                DateTimeOffset.Parse("2026-03-20T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-04-05T12:00:00.000Z", CultureInfo.InvariantCulture),
                "INMET / orgaos meteorologicos oficiais",
                true
            ),
            new(
                "El Nino sera declarado ativo por agencia internacional em 2026?",
                "Resolve SIM se a agencia definida declarar oficialmente condicoes de El Nino em algum momento de 2026.",
                "CLIMA",
                new[] { "el-nino", "noaa", "clima" },
                35,
                DateTimeOffset.Parse("2026-12-10T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2027-01-20T12:00:00.000Z", CultureInfo.InvariantCulture),
                "NOAA/CPC ou agencia internacional equivalente",
                false
            ),
            new(
                "Ibovespa fechara acima de 150.000 pontos em 30/06/2026?",
                "Resolve SIM se o valor de fechamento do Ibovespa na data de resolucao for > 150.000 pontos.",
                "FINANCAS",
                new[] { "ibovespa", "bolsa", "b3" },
                29,
                DateTimeOffset.Parse("2026-06-25T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-06-30T20:30:00.000Z", CultureInfo.InvariantCulture),
                "B3 (cotacao oficial do Ibovespa)",
                true
            ),
            new(
                "Dolar (PTAX) ficara abaixo de R$ 5,50 em 30/06/2026?",
                "Resolve SIM se a PTAX de fechamento na data de resolucao for < 5,50.",
                "FINANCAS",
                new[] { "dolar", "ptax", "cambio" },
                34,
                DateTimeOffset.Parse("2026-06-25T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-06-30T14:00:00.000Z", CultureInfo.InvariantCulture),
                "Banco Central do Brasil (PTAX)",
                false
            ),
            new(
                "Uma Big Tech anunciara investimento bilionario (R$) no Brasil ate 31/12/2026?",
                "Resolve SIM se uma Big Tech (definida no enunciado) anunciar publicamente investimento >= R$ 1 bilhao no Brasil ate 31/12/2026.",
                "EMPRESAS",
                new[] { "big-tech", "investimento", "brasil" },
                40,
                DateTimeOffset.Parse("2026-12-10T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2027-01-05T12:00:00.000Z", CultureInfo.InvariantCulture),
                "Comunicado oficial da empresa + imprensa confiavel",
                true
            ),
            new(
                "Uma empresa brasileira fara IPO na B3 ate 31/12/2026?",
                "Resolve SIM se ocorrer ao menos 1 IPO de empresa brasileira na B3 ate 31/12/2026.",
                "EMPRESAS",
                new[] { "ipo", "b3", "mercado" },
                32,
                DateTimeOffset.Parse("2026-12-10T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2027-01-10T12:00:00.000Z", CultureInfo.InvariantCulture),
                "B3 (comunicados e registros de listagem)",
                false
            ),
            new(
                "Brasil aprovara uma nova lei relevante de IA ate 31/12/2026?",
                "Resolve SIM se uma lei federal sobre IA (definida no enunciado) for sancionada ate 31/12/2026.",
                "TECNOLOGIA-E-CIENCIA",
                new[] { "ia", "lei", "congresso" },
                44,
                DateTimeOffset.Parse("2026-12-10T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2027-01-10T12:00:00.000Z", CultureInfo.InvariantCulture),
                "Planalto + Diario Oficial + tramitacao oficial",
                true
            ),
            new(
                "PIX ganhara nova funcionalidade oficial ate 31/05/2026?",
                "Resolve SIM se o Banco Central anunciar e lancar publicamente nova funcionalidade do PIX ate 31/05/2026.",
                "TECNOLOGIA-E-CIENCIA",
                new[] { "pix", "bacen", "fintech" },
                60,
                DateTimeOffset.Parse("2026-05-20T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-06-01T12:00:00.000Z", CultureInfo.InvariantCulture),
                "Banco Central do Brasil (comunicados oficiais)",
                false
            ),
            new(
                "Anvisa aprovara uma nova vacina de uso amplo ate 30/09/2026?",
                "Resolve SIM se a Anvisa publicar aprovacao de registro para vacina com indicacao de uso amplo ate 30/09/2026.",
                "SAUDE",
                new[] { "anvisa", "vacina", "registro" },
                27,
                DateTimeOffset.Parse("2026-09-20T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-10-05T12:00:00.000Z", CultureInfo.InvariantCulture),
                "Anvisa (consultas e publicacoes oficiais)",
                false
            ),
            new(
                "SUS anunciara expansao nacional de um programa de saude ate 31/12/2026?",
                "Resolve SIM se o Ministerio da Saude anunciar e publicar expansao nacional de programa definido no enunciado ate 31/12/2026.",
                "SAUDE",
                new[] { "sus", "ministerio-da-saude", "programa" },
                45,
                DateTimeOffset.Parse("2026-12-10T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2027-01-10T12:00:00.000Z", CultureInfo.InvariantCulture),
                "Ministerio da Saude (atos e comunicados oficiais)",
                true
            ),
            new(
                "Um conflito internacional relevante tera cessar-fogo formal ate 30/06/2026?",
                "Resolve SIM se houver anuncio formal de cessar-fogo em conflito definido no enunciado ate 30/06/2026.",
                "MUNDO",
                new[] { "geopolitica", "cessar-fogo", "mundo" },
                36,
                DateTimeOffset.Parse("2026-06-20T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-07-05T12:00:00.000Z", CultureInfo.InvariantCulture),
                "ONU + comunicados oficiais das partes",
                false
            ),
            new(
                "Um pais do G20 entrara oficialmente em recessao tecnica em 2026?",
                "Resolve SIM se um pais do G20 registrar dois trimestres seguidos de queda do PIB (conforme estatistica oficial) em 2026.",
                "MUNDO",
                new[] { "g20", "recessao", "pib" },
                42,
                DateTimeOffset.Parse("2026-12-10T20:00:00.000Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2027-02-01T12:00:00.000Z", CultureInfo.InvariantCulture),
                "Institutos oficiais de estatistica/BCs dos paises (dados de PIB)",
                false
            ),
        };
    }

    private async Task FixCategoryNamesAsync(CancellationToken ct)
    {
        var canonical = new[]
        {
            "EM-ALTA",
            "NOVIDADES",
            "TODAS",
            "POLITICA",
            "ESPORTES",
            "CULTURA",
            "CRIPTOMOEDAS",
            "CLIMA",
            "ECONOMIA",
            "MENCOES",
            "EMPRESAS",
            "FINANCAS",
            "TECNOLOGIA-E-CIENCIA",
            "SAUDE",
            "MUNDO",
        };

        var map = canonical.ToDictionary(
            name => RemoveDiacritics(name).Trim().ToUpperInvariant(),
            name => name
        );

        var categories = await _db.Categories.ToListAsync(ct);
        if (categories.Count == 0)
        {
            return;
        }

        var changed = false;
        foreach (var category in categories)
        {
            var key = RemoveDiacritics(category.Name ?? string.Empty).Trim().ToUpperInvariant();
            if (!map.TryGetValue(key, out var canonicalName))
            {
                continue;
            }

            if (!string.Equals(category.Name, canonicalName, StringComparison.Ordinal))
            {
                category.Name = canonicalName;
                category.Slug = SlugifyCategory(canonicalName);
                changed = true;
            }
        }

        // Remove duplicates after normalization (keep lowest Id)
        var duplicates = categories
            .GroupBy(c => (c.Name ?? string.Empty).Trim().ToUpperInvariant())
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in duplicates)
        {
            var keep = group.OrderBy(c => c.Id).First();
            var remove = group.Where(c => c.Id != keep.Id).ToList();
            if (remove.Count > 0)
            {
                var removeIds = remove.Select(c => c.Id).ToList();
                var links = await _db
                    .MarketCategories.Where(mc => removeIds.Contains(mc.CategoryId))
                    .ToListAsync(ct);
                foreach (var link in links)
                {
                    link.CategoryId = keep.Id;
                }
                _db.Categories.RemoveRange(remove);
                changed = true;
            }
        }

        if (changed)
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    private static string NormalizeCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return "ECONOMIA";
        }

        var normalized = RemoveDiacritics(category).Trim().ToUpperInvariant();
        normalized = Regex.Replace(normalized, "\\s+", " ");

        return normalized;
    }

    private static string SlugifyCategory(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = RemoveDiacritics(input).Trim();
        var collapsed = Regex.Replace(normalized, "\\s+", "-");
        var cleaned = Regex.Replace(collapsed, "[^A-Za-z0-9\\-]", string.Empty);
        return cleaned.ToUpperInvariant();
    }

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
