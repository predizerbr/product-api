using Product.Common.Entities;
using Product.Common.Enums;

namespace Product.Data.Models.Wallet;

public class LedgerEntry : Entity<Guid>
{
    public Guid AccountId { get; set; }
    public LedgerEntryType Type { get; set; }
    public decimal Amount { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? MetaJson { get; set; }
    public Account? Account { get; set; }
}
