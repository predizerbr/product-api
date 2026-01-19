using Microsoft.EntityFrameworkCore;
using Product.Data.Models.Users.PaymentsMethods;
using Product.Data.Models.Wallet;

namespace Product.Data.Database.Contexts;

public partial class AppDbContext
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<UserCard> UserCards => Set<UserCard>();
    public DbSet<UserBankAccount> UserBankAccounts => Set<UserBankAccount>();
    public DbSet<UserPixKey> UserPixKeys => Set<UserPixKey>();
    public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();
    public DbSet<Withdrawal> Withdrawals => Set<Withdrawal>();
}
