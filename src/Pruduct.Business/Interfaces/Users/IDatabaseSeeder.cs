using Microsoft.Extensions.Configuration;

namespace Pruduct.Business.Abstractions;

public interface IDatabaseSeeder
{
    Task SeedAsync(IConfiguration configuration, CancellationToken ct = default);
}
