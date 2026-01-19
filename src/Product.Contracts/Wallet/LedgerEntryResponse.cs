namespace Product.Contracts.Wallet;

public class LedgerEntryResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = default!;
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
