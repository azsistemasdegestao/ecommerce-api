using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bogus;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Enums;
using Ecommerce.Domain.Interfaces;
using Ecommerce.IntegrationTests.Infrastructure;
using Ecommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Ecommerce.IntegrationTests.Payments;

[Collection("PaymentsEndpoints")]
public class PaymentsEndpointsTests : IClassFixture<TestContainersFixture>
{
    private static readonly Faker _faker = new();
    private readonly TestContainersFixture _containers;

    public PaymentsEndpointsTests(TestContainersFixture containers)
    {
        _containers = containers;
    }

    private sealed class FakeGatewayService : IMockGatewayService
    {
        private readonly bool _success;
        public FakeGatewayService(bool success) => _success = success;

        public Task<GatewayResult> ProcessAsync(Guid paymentId, decimal amount, PaymentMethod paymentMethod, CancellationToken ct = default) =>
            Task.FromResult(_success ? new GatewayResult(true, null) : new GatewayResult(false, "Insufficient funds"));
    }

    private Task<(CustomWebApplicationFactory Factory, HttpClient Client)> CreateClientAsync(bool? gatewaySuccess = null)
    {
        var factory = new CustomWebApplicationFactory
        {
            PostgresConnectionString = _containers.Postgres.GetConnectionString(),
            RedisConnectionString = _containers.Redis.GetConnectionString(),
            MinioEndpoint = _containers.Minio.GetConnectionString(),
            MinioAccessKey = _containers.Minio.GetAccessKey(),
            MinioSecretKey = _containers.Minio.GetSecretKey(),
            ConfigureTestServices = gatewaySuccess is null
                ? null
                : services =>
                {
                    services.RemoveAll<IMockGatewayService>();
                    services.AddScoped<IMockGatewayService>(_ => new FakeGatewayService(gatewaySuccess.Value));
                }
        };

        return Task.FromResult((factory, factory.CreateClient()));
    }

    private static async Task<Guid> CreateProductDirectAsync(
        CustomWebApplicationFactory factory, string name, string slug, decimal price = 29.90m, int stock = 10)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var category = Category.Create(_faker.Commerce.Department(), _faker.Lorem.Slug());
        db.Set<Category>().Add(category);
        var product = Product.Create(name, "description", slug, price, stock, "https://example.com/img.png", category.Id);
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<string> CreateCustomerAndLoginAsync(HttpClient client)
    {
        var email = _faker.Internet.Email();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            first_name = "Test", last_name = "Customer", email, password = "Password@123"
        });

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password@123" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("access_token").GetString()!;
    }

    private static void Authorize(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    private static async Task<Guid> CheckoutAsync(HttpClient client, CustomWebApplicationFactory factory, string seed)
    {
        var productId = await CreateProductDirectAsync(factory, seed, seed);
        await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity = 1 });
        var response = await client.PostAsJsonAsync("/api/v1/orders", new { shipping_address = "123 Main St" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    // AC-PAY-I01
    [Fact]
    public async Task Should_Return_202_When_RequestPayment_With_Valid_Pending_Order()
    {
        var (factory, client) = await CreateClientAsync(gatewaySuccess: true);
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "pay-shoes");

        var response = await client.PostAsJsonAsync("/api/v1/payments", new { order_id = orderId, payment_method = "CreditCard" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        body.GetProperty("payment_id").GetGuid().Should().NotBe(Guid.Empty);
    }

    // AC-PAY-I02
    [Fact]
    public async Task Should_Return_422_When_RequestPayment_For_Confirmed_Order()
    {
        var (factory, client) = await CreateClientAsync(gatewaySuccess: true);
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "pay-cap");
        await client.PostAsJsonAsync("/api/v1/payments", new { order_id = orderId, payment_method = "CreditCard" }); // confirms the order via the synchronous event chain

        var response = await client.PostAsJsonAsync("/api/v1/payments", new { order_id = orderId, payment_method = "CreditCard" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // AC-PAY-I03
    [Fact]
    public async Task Should_Return_401_When_RequestPayment_Without_Jwt()
    {
        var (_, client) = await CreateClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/payments", new { order_id = Guid.NewGuid(), payment_method = "CreditCard" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // AC-PAY-I04
    [Fact]
    public async Task Should_Return_404_When_RequestPayment_For_NonExisting_Order()
    {
        var (_, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        var response = await client.PostAsJsonAsync("/api/v1/payments", new { order_id = Guid.NewGuid(), payment_method = "CreditCard" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // AC-PAY-I05
    [Fact]
    public async Task Should_Return_422_When_RequestPayment_For_Another_Customers_Order()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "pay-belt");

        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var response = await client.PostAsJsonAsync("/api/v1/payments", new { order_id = orderId, payment_method = "CreditCard" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // AC-PAY-I06
    [Fact]
    public async Task Should_Return_200_With_Processed_Status_After_Approval()
    {
        var (factory, client) = await CreateClientAsync(gatewaySuccess: true);
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "pay-watch");
        await client.PostAsJsonAsync("/api/v1/payments", new { order_id = orderId, payment_method = "CreditCard" });

        var response = await client.GetAsync($"/api/v1/payments/{orderId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("status").GetString().Should().Be("Processed");
    }

    // AC-PAY-I14
    [Fact]
    public async Task Should_Return_400_When_RequestPayment_With_Invalid_PaymentMethod()
    {
        var (factory, client) = await CreateClientAsync(gatewaySuccess: true);
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "pay-headband");

        var response = await client.PostAsJsonAsync("/api/v1/payments", new { order_id = orderId, payment_method = "Bitcoin" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // AC-PAY-I15
    [Fact]
    public async Task Should_Return_The_Chosen_PaymentMethod_When_Checking_Payment_Status()
    {
        var (factory, client) = await CreateClientAsync(gatewaySuccess: true);
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "pay-beanie");
        await client.PostAsJsonAsync("/api/v1/payments", new { order_id = orderId, payment_method = "Pix" });

        var response = await client.GetAsync($"/api/v1/payments/{orderId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("payment_method").GetString().Should().Be("Pix");
    }

    // AC-PAY-I07
    [Fact]
    public async Task Should_Return_200_With_Failed_Status_After_Failure()
    {
        var (factory, client) = await CreateClientAsync(gatewaySuccess: false);
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "pay-wallet");
        await client.PostAsJsonAsync("/api/v1/payments", new { order_id = orderId, payment_method = "CreditCard" });

        var response = await client.GetAsync($"/api/v1/payments/{orderId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("status").GetString().Should().Be("Failed");
    }

    // AC-PAY-I08
    [Fact]
    public async Task Should_Return_401_When_GetPaymentByOrderId_Without_Jwt()
    {
        var (_, client) = await CreateClientAsync();

        var response = await client.GetAsync($"/api/v1/payments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // AC-PAY-I09
    [Fact]
    public async Task Should_Return_403_When_GetPaymentByOrderId_For_Another_Customers_Order()
    {
        var (factory, client) = await CreateClientAsync(gatewaySuccess: true);
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "pay-gloves");
        await client.PostAsJsonAsync("/api/v1/payments", new { order_id = orderId, payment_method = "CreditCard" });

        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var response = await client.GetAsync($"/api/v1/payments/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // AC-PAY-I10
    [Fact]
    public async Task Should_Confirm_Order_When_Full_Flow_Is_Approved()
    {
        var (factory, client) = await CreateClientAsync(gatewaySuccess: true);
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "pay-scarf");
        await client.PostAsJsonAsync("/api/v1/payments", new { order_id = orderId, payment_method = "CreditCard" });

        var response = await client.GetAsync($"/api/v1/orders/{orderId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("status").GetString().Should().Be("Confirmed");
    }

    // AC-PAY-I11
    [Fact]
    public async Task Should_Cancel_Order_When_Full_Flow_Is_Rejected()
    {
        var (factory, client) = await CreateClientAsync(gatewaySuccess: false);
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "pay-umbrella");
        await client.PostAsJsonAsync("/api/v1/payments", new { order_id = orderId, payment_method = "CreditCard" });

        var response = await client.GetAsync($"/api/v1/orders/{orderId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("status").GetString().Should().Be("Cancelled");
    }

    // AC-PAY-I12
    [Fact]
    public async Task Should_Return_429_When_Payment_Rate_Limit_Exceeded()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        HttpResponseMessage response = null!;
        for (var i = 0; i < 11; i++)
        {
            response = await client.PostAsJsonAsync("/api/v1/payments", new { order_id = Guid.NewGuid(), payment_method = "CreditCard" });
        }

        response.StatusCode.Should().Be((HttpStatusCode)429);
        _ = factory;
    }

    // AC-PAY-I13
    [Fact]
    public async Task Should_Return_422_When_RequestPayment_Twice_For_Same_Order()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "pay-socks");

        await client.PostAsJsonAsync("/api/v1/payments", new { order_id = orderId, payment_method = "CreditCard" });
        var second = await client.PostAsJsonAsync("/api/v1/payments", new { order_id = orderId, payment_method = "CreditCard" });

        second.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
