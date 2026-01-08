using Microsoft.EntityFrameworkCore;
using Pruduct.Data.Models;

namespace Pruduct.Data.Database.Contexts;

public partial class AppDbContext
{
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
}
