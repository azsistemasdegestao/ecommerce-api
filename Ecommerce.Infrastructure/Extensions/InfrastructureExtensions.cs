using System.Data;
using Ecommerce.Application.Admin;
using Ecommerce.Application.Cart;
using Ecommerce.Application.Catalog;
using Ecommerce.Application.Orders;
using Ecommerce.Application.Payments;
using Ecommerce.Infrastructure.Payments;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using Ecommerce.Infrastructure.Auth;
using Ecommerce.Infrastructure.Cache;
using Ecommerce.Infrastructure.Cache.Handlers;
using Ecommerce.Infrastructure.Email;
using Ecommerce.Infrastructure.EventBus;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Infrastructure.Persistence.Repositories;
using Ecommerce.Infrastructure.Queries;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ecommerce.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["DB_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("DB_CONNECTION_STRING is not configured.");

        var redisConnectionString = configuration["REDIS_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("REDIS_CONNECTION_STRING is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());

        services.AddStackExchangeRedisCache(options =>
            options.Configuration = redisConnectionString);

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<IEventBus, InMemoryEventBus>();

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;

            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        // BR-AUTH-016: password reset token valid for 1 hour (default DataProtectorTokenProvider is 1 day).
        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(1));

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailService, MockEmailService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();

        services.AddScoped<IDbConnection>(_ => new NpgsqlConnection(connectionString));
        services.AddScoped<IAdminQueryService, AdminQueryService>();

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductQueryService, ProductQueryService>();

        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<ICartQueryService, CartQueryService>();

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderQueryService, OrderQueryService>();

        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentQueryService, PaymentQueryService>();
        services.AddScoped<IMockGatewayService, MockGatewayService>();

        services.AddScoped<IEventHandler<ProductUpdated>, ProductUpdatedCacheHandler>();
        services.AddScoped<IEventHandler<ProductDeleted>, ProductDeletedCacheHandler>();

        return services;
    }
}
