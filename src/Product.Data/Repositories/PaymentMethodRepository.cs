using Microsoft.EntityFrameworkCore;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Users.PaymentsMethods;

namespace Product.Data.Repositories;

public class PaymentMethodRepository(AppDbContext db) : IPaymentMethodRepository
{
    public async Task<(
        List<UserCard> Cards,
        List<UserBankAccount> Banks,
        List<UserPixKey> Pix
    )> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var cards = await db.UserCards.Where(x => x.UserId == userId).ToListAsync(ct);
        var banks = await db.UserBankAccounts.Where(x => x.UserId == userId).ToListAsync(ct);
        var pix = await db.UserPixKeys.Where(x => x.UserId == userId).ToListAsync(ct);

        return (cards, banks, pix);
    }

    public async Task<(
        List<UserCard> Cards,
        List<UserBankAccount> Banks,
        List<UserPixKey> Pix
    )> GetDefaultsAsync(Guid userId, CancellationToken ct = default)
    {
        var cards = await db
            .UserCards.Where(x => x.UserId == userId && x.IsDefault)
            .ToListAsync(ct);
        var banks = await db
            .UserBankAccounts.Where(x => x.UserId == userId && x.IsDefault)
            .ToListAsync(ct);
        var pix = await db
            .UserPixKeys.Where(x => x.UserId == userId && x.IsDefault)
            .ToListAsync(ct);

        return (cards, banks, pix);
    }

    public async Task<(UserCard? Card, UserBankAccount? Bank, UserPixKey? Pix)> GetByIdAsync(
        Guid userId,
        Guid methodId,
        CancellationToken ct = default
    )
    {
        var card = await db.UserCards.FirstOrDefaultAsync(
            x => x.Id == methodId && x.UserId == userId,
            ct
        );
        if (card is not null)
            return (card, null, null);

        var bank = await db.UserBankAccounts.FirstOrDefaultAsync(
            x => x.Id == methodId && x.UserId == userId,
            ct
        );
        if (bank is not null)
            return (null, bank, null);

        var pix = await db.UserPixKeys.FirstOrDefaultAsync(
            x => x.Id == methodId && x.UserId == userId,
            ct
        );
        if (pix is not null)
            return (null, null, pix);

        return (null, null, null);
    }

    public async Task AddUserCardAsync(UserCard card, CancellationToken ct = default)
    {
        if (card.IsDefault)
        {
            await UnsetAllDefaultsAsync(card.UserId, ct);
        }

        db.UserCards.Add(card);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> HasAnyAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.UserCards.AnyAsync(x => x.UserId == userId, ct)
            || await db.UserBankAccounts.AnyAsync(x => x.UserId == userId, ct)
            || await db.UserPixKeys.AnyAsync(x => x.UserId == userId, ct);
    }

    public async Task AddUserBankAccountAsync(UserBankAccount bank, CancellationToken ct = default)
    {
        if (bank.IsDefault)
        {
            await UnsetAllDefaultsAsync(bank.UserId, ct);
        }

        db.UserBankAccounts.Add(bank);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddUserPixKeyAsync(UserPixKey pix, CancellationToken ct = default)
    {
        if (pix.IsDefault)
        {
            await UnsetAllDefaultsAsync(pix.UserId, ct);
        }

        db.UserPixKeys.Add(pix);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateUserCardsAsync(
        IEnumerable<UserCard> cards,
        CancellationToken ct = default
    )
    {
        foreach (var c in cards)
        {
            db.UserCards.Update(c);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateUserBankAccountsAsync(
        IEnumerable<UserBankAccount> banks,
        CancellationToken ct = default
    )
    {
        foreach (var b in banks)
        {
            db.UserBankAccounts.Update(b);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateUserPixKeysAsync(
        IEnumerable<UserPixKey> pixKeys,
        CancellationToken ct = default
    )
    {
        foreach (var p in pixKeys)
        {
            db.UserPixKeys.Update(p);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveByIdAsync(Guid userId, Guid methodId, CancellationToken ct = default)
    {
        var card = await db.UserCards.FirstOrDefaultAsync(
            x => x.Id == methodId && x.UserId == userId,
            ct
        );
        if (card is not null)
        {
            db.UserCards.Remove(card);
            await db.SaveChangesAsync(ct);
            return;
        }

        var bank = await db.UserBankAccounts.FirstOrDefaultAsync(
            x => x.Id == methodId && x.UserId == userId,
            ct
        );
        if (bank is not null)
        {
            db.UserBankAccounts.Remove(bank);
            await db.SaveChangesAsync(ct);
            return;
        }

        var pix = await db.UserPixKeys.FirstOrDefaultAsync(
            x => x.Id == methodId && x.UserId == userId,
            ct
        );
        if (pix is not null)
        {
            db.UserPixKeys.Remove(pix);
            await db.SaveChangesAsync(ct);
            return;
        }
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
    }

    public async Task<UserCard?> GetUserCardByMpCardIdAsync(
        Guid userId,
        string mpCardId,
        CancellationToken ct = default
    )
    {
        return await db.UserCards.FirstOrDefaultAsync(
            x => x.UserId == userId && x.MpCardId == mpCardId,
            ct
        );
    }

    private async Task UnsetAllDefaultsAsync(Guid userId, CancellationToken ct = default)
    {
        var cardDefaults = await db
            .UserCards.Where(x => x.UserId == userId && x.IsDefault)
            .ToListAsync(ct);
        foreach (var c in cardDefaults)
        {
            c.IsDefault = false;
            db.UserCards.Update(c);
        }

        var bankDefaults = await db
            .UserBankAccounts.Where(x => x.UserId == userId && x.IsDefault)
            .ToListAsync(ct);
        foreach (var b in bankDefaults)
        {
            b.IsDefault = false;
            db.UserBankAccounts.Update(b);
        }

        var pixDefaults = await db
            .UserPixKeys.Where(x => x.UserId == userId && x.IsDefault)
            .ToListAsync(ct);
        foreach (var p in pixDefaults)
        {
            p.IsDefault = false;
            db.UserPixKeys.Update(p);
        }

        await db.SaveChangesAsync(ct);
    }
}
