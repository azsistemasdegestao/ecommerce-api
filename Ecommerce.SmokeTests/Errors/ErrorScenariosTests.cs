using System.Net;
using Ecommerce.SmokeTests.Infrastructure;
using FluentAssertions;

namespace Ecommerce.SmokeTests.Errors;

[Collection("Smoke API")]
public sealed class ErrorScenariosTests
{
    private readonly SmokeApiFixture _fixture;

    public ErrorScenariosTests(SmokeApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Should_Return400_When_PageSizeExceedsMaximum()
    {
        var response = await _fixture.Client.SendJsonAsync(HttpMethod.Get, "/api/v1/catalog/products?page_size=999");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_Return404_When_ProductSlugDoesNotExist()
    {
        var response = await _fixture.Client.SendJsonAsync(
            HttpMethod.Get, $"/api/v1/catalog/products/does-not-exist-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_Return404_When_OrderDoesNotExist()
    {
        var response = await _fixture.Client.SendJsonAsync(
            HttpMethod.Get, $"/api/v1/orders/{Guid.NewGuid()}", _fixture.CustomerAccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_Return422_When_AddingOutOfStockProductToCart()
    {
        var response = await _fixture.Client.SendJsonAsync(HttpMethod.Post, "/api/v1/cart/items", new
        {
            product_id = _fixture.OutOfStockProductId,
            quantity = 1
        }, _fixture.CustomerAccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
