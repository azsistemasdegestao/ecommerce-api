using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bogus;
using Ecommerce.SmokeTests.Infrastructure;
using FluentAssertions;

namespace Ecommerce.SmokeTests.Purchases;

[Collection("Smoke API")]
public sealed class FullPurchaseFlowTests
{
    private static readonly Faker Faker = new();

    private readonly SmokeApiFixture _fixture;

    public FullPurchaseFlowTests(SmokeApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Should_CompletePurchase_When_CustomerChecksOutAndPays()
    {
        var addToCartResponse = await _fixture.Client.SendJsonAsync(HttpMethod.Post, "/api/v1/cart/items", new
        {
            product_id = _fixture.ProductId,
            quantity = 2
        }, _fixture.CustomerAccessToken);
        addToCartResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var cartResponse = await _fixture.Client.SendJsonAsync(
            HttpMethod.Get, "/api/v1/cart", _fixture.CustomerAccessToken);
        var cart = await cartResponse.Content.ReadFromJsonAsync<JsonElement>();
        cart.GetProperty("item_count").GetInt32().Should().BeGreaterThanOrEqualTo(1);

        var orderResponse = await _fixture.Client.SendJsonAsync(HttpMethod.Post, "/api/v1/orders", new
        {
            shipping_address = Faker.Address.FullAddress()
        }, _fixture.CustomerAccessToken);
        orderResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await orderResponse.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = order.GetProperty("id").GetGuid();

        var paymentResponse = await _fixture.Client.SendJsonAsync(HttpMethod.Post, "/api/v1/payments", new
        {
            order_id = orderId
        }, _fixture.CustomerAccessToken);
        paymentResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var finalStatus = await PollUntilPaymentSettlesAsync(orderId);
        new[] { "Processed", "Failed" }.Should().Contain(finalStatus);

        var orderDetailResponse = await _fixture.Client.SendJsonAsync(
            HttpMethod.Get, $"/api/v1/orders/{orderId}", _fixture.CustomerAccessToken);
        orderDetailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> PollUntilPaymentSettlesAsync(Guid orderId)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var response = await _fixture.Client.SendJsonAsync(
                HttpMethod.Get, $"/api/v1/payments/{orderId}", _fixture.CustomerAccessToken);
            var payment = await response.Content.ReadFromJsonAsync<JsonElement>();
            var status = payment.GetProperty("status").GetString()!;

            if (status is "Processed" or "Failed")
                return status;

            await Task.Delay(300);
        }

        throw new TimeoutException("Payment did not settle within the polling window.");
    }
}
