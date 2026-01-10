using Microsoft.Extensions.Configuration;

namespace Pruduct.Business.Interfaces.Users;

public interface IDatabaseSeeder
{
    Task SeedAsync(IConfiguration configuration, CancellationToken ct = default);
}
