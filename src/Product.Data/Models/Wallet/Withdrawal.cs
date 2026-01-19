using Product.Common.Entities;
using Product.Common.Enums;
using Product.Data.Models.Users;

namespace Product.Data.Models.Wallet;

public class Withdrawal : Entity<Guid>
{
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BRL";
    public WithdrawalStatus Status { get; set; } = WithdrawalStatus.REQUESTED;
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? Notes { get; set; }
    public string IdempotencyKey { get; set; } = default!;
    public User? User { get; set; }
}
