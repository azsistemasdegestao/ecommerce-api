using System.Data;
using Amazon.S3;
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
using Ecommerce.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;

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

        var minioEndpoint = configuration["MINIO_ENDPOINT"]
            ?? throw new InvalidOperationException("MINIO_ENDPOINT is not configured.");
        var minioAccessKey = configuration["MINIO_ROOT_USER"]
            ?? throw new InvalidOperationException("MINIO_ROOT_USER is not configured.");
        var minioSecretKey = configuration["MINIO_ROOT_PASSWORD"]
            ?? throw new InvalidOperationException("MINIO_ROOT_PASSWORD is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());

        var redisMultiplexer = ConnectionMultiplexer.Connect(redisConnectionString);
        services.AddSingleton<IConnectionMultiplexer>(redisMultiplexer);

        services.AddStackExchangeRedisCache(options =>
            options.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(redisMultiplexer));

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<IEventBus, InMemoryEventBus>();

        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
            minioAccessKey,
            minioSecretKey,
            new AmazonS3Config { ServiceURL = minioEndpoint, ForcePathStyle = true }));
        services.AddSingleton<IImageStorageService, S3ImageStorageService>();

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

        services.AddScoped<IEventHandler<ProductCreated>, ProductCreatedCacheHandler>();
        services.AddScoped<IEventHandler<ProductUpdated>, ProductUpdatedCacheHandler>();
        services.AddScoped<IEventHandler<ProductDeleted>, ProductDeletedCacheHandler>();

        return services;
    }
}
