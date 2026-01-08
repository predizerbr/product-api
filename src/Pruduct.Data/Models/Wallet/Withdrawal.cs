using Pruduct.Common.Entities;
using Pruduct.Common.Enums;

namespace Pruduct.Data.Models;

public class Withdrawal : Entity<Guid>
{
    public Guid UserId { get; set; }
    public long Amount { get; set; }
    public string Currency { get; set; } = "BRL";
    public WithdrawalStatus Status { get; set; } = WithdrawalStatus.REQUESTED;
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? Notes { get; set; }
    public string IdempotencyKey { get; set; } = default!;
    public User? User { get; set; }
}
