using Microsoft.EntityFrameworkCore;
using Pruduct.Data.Models.Emails;

namespace Pruduct.Data.Database.Contexts;

public partial class AppDbContext
{
    public DbSet<QueuedEmail> QueuedEmails => Set<QueuedEmail>();
}
