using Pruduct.Api.Configuration;
using Pruduct.Business.Interfaces.Users;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(
    (ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console()
);

builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

app.UseApiPipeline();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
    await seeder.SeedAsync(app.Configuration);
}

// Auth endpoints are provided by AuthController now (MVC controllers are registered in AddApiServices)

app.Run();
