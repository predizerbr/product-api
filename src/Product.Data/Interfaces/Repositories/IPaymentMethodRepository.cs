using System.Collections.Generic;
using Product.Data.Models.Users.PaymentsMethods;

namespace Product.Data.Interfaces.Repositories;

public interface IPaymentMethodRepository
{
    // Returns separate lists for each payment-method type
    Task<(List<UserCard> Cards, List<UserBankAccount> Banks, List<UserPixKey> Pix)> GetByUserAsync(
        Guid userId,
        CancellationToken ct = default
    );
    Task<bool> HasAnyAsync(Guid userId, CancellationToken ct = default);
    Task<(
        List<UserCard> Cards,
        List<UserBankAccount> Banks,
        List<UserPixKey> Pix
    )> GetDefaultsAsync(Guid userId, CancellationToken ct = default);

    // Returns the matching entity in a discriminated tuple (only one non-null)
    Task<(UserCard? Card, UserBankAccount? Bank, UserPixKey? Pix)> GetByIdAsync(
        Guid userId,
        Guid methodId,
        CancellationToken ct = default
    );

    Task AddUserCardAsync(UserCard card, CancellationToken ct = default);
    Task AddUserBankAccountAsync(UserBankAccount bank, CancellationToken ct = default);
    Task AddUserPixKeyAsync(UserPixKey pix, CancellationToken ct = default);

    Task RemoveByIdAsync(Guid userId, Guid methodId, CancellationToken ct = default);

    Task UpdateUserCardsAsync(IEnumerable<UserCard> cards, CancellationToken ct = default);
    Task UpdateUserBankAccountsAsync(
        IEnumerable<UserBankAccount> banks,
        CancellationToken ct = default
    );
    Task UpdateUserPixKeysAsync(IEnumerable<UserPixKey> pix, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    Task<UserCard?> GetUserCardByMpCardIdAsync(
        Guid userId,
        string mpCardId,
        CancellationToken ct = default
    );
}
