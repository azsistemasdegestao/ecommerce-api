using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.IntegrationTests.Infrastructure;

// Program.cs reads configuration eagerly (AddInfrastructure(builder.Configuration) runs
// before app.Build()), so ConfigureWebHost's ConfigureAppConfiguration hook — which is wired
// up by WebApplicationFactory's HostFactoryResolver — applies too late to influence those
// reads. Process environment variables, by contrast, are visible the moment
// WebApplication.CreateBuilder(args) runs, so they are the reliable way to inject config here.
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public required string PostgresConnectionString { get; init; }
    public required string RedisConnectionString { get; init; }
    public Action<IServiceCollection>? ConfigureTestServices { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" avoids loading appsettings.Development.json, whose hardcoded
        // DB_CONNECTION_STRING/REDIS_CONNECTION_STRING point at the local docker-compose stack.
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", PostgresConnectionString);
        Environment.SetEnvironmentVariable("REDIS_CONNECTION_STRING", RedisConnectionString);
        Environment.SetEnvironmentVariable("JWT_SECRET", "integration-test-secret-key-minimum-32-chars!!");
        Environment.SetEnvironmentVariable("JWT_ISSUER", "ecommerce-api");
        Environment.SetEnvironmentVariable("JWT_AUDIENCE", "ecommerce-client");
        Environment.SetEnvironmentVariable("SEQ_URL", "http://localhost:5341");
        Environment.SetEnvironmentVariable("JAEGER_ENDPOINT", "http://localhost:4317");

        if (ConfigureTestServices is not null)
            builder.ConfigureServices(ConfigureTestServices);
    }
}
