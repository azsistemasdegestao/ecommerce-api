using Ecommerce.API.Endpoints.Admin;
using Ecommerce.API.Endpoints.Auth;
using Ecommerce.API.Endpoints.Cart;
using Ecommerce.API.Endpoints.Catalog;
using Ecommerce.API.Endpoints.Orders;
using Ecommerce.API.Endpoints.Payments;
using Ecommerce.API.Extensions;
using Ecommerce.API.Middleware;
using Ecommerce.Application.Extensions;
using Ecommerce.Infrastructure.Extensions;
using Ecommerce.Infrastructure.Seeding;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .WriteTo.Console()
       .WriteTo.Seq(ctx.Configuration["SEQ_URL"] ?? "http://localhost:5341")
       .WriteTo.GrafanaLoki(
           ctx.Configuration["LOKI_URL"] ?? "http://localhost:3100",
           labels: [new LokiLabel { Key = "app", Value = "ecommerce-api" }]));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddApiRateLimiting();
builder.Services.AddApiHealthChecks(builder.Configuration);
builder.Services.AddObservability(builder.Configuration);
builder.Services.AddApiCors(builder.Configuration);
builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower);

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseSerilogRequestLogging();
app.UseSecurityHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("Ecommerce API"));
}

app.UseHttpsRedirection();
app.UseCors(SecurityExtensions.CorsPolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthCheckEndpoints();
app.MapPrometheusScrapingEndpoint("/metrics");
app.MapAuthEndpoints();
app.MapAdminUsersEndpoints();
app.MapAdminCategoriesEndpoints();
app.MapAdminOrdersEndpoints();
app.MapAdminPaymentsEndpoints();
app.MapCatalogEndpoints();
app.MapCartEndpoints();
app.MapOrdersEndpoints();
app.MapPaymentsEndpoints();

using (var scope = app.Services.CreateScope())
{
    await BucketInitializer.EnsureBucketExistsAsync(scope.ServiceProvider);
    await RoleSeeder.SeedRolesAsync(scope.ServiceProvider);
    await AdminSeeder.SeedAsync(scope.ServiceProvider);
}

app.Run();

public partial class Program { }
