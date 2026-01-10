using Microsoft.EntityFrameworkCore;
using Pruduct.Data.Models.Payments;
using Pruduct.Data.Models.Wallet;

namespace Pruduct.Data.Database.Contexts;

public partial class AppDbContext
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();
    public DbSet<Withdrawal> Withdrawals => Set<Withdrawal>();
}
