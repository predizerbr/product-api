using Pruduct.Common.Entities;

namespace Pruduct.Data.Models;

public class Account : Entity<Guid>
{
    public Guid UserId { get; set; }
    public string Currency { get; set; } = "BRL";
    public User? User { get; set; }
    public ICollection<LedgerEntry> LedgerEntries { get; set; } = new List<LedgerEntry>();
}
