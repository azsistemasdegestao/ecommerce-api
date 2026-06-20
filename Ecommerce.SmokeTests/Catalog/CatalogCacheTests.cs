using System.Net;
using Ecommerce.SmokeTests.Infrastructure;
using FluentAssertions;

namespace Ecommerce.SmokeTests.Catalog;

[Collection("Smoke API")]
public sealed class CatalogCacheTests
{
    private const int Repetitions = 15;

    private readonly SmokeApiFixture _fixture;

    public CatalogCacheTests(SmokeApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Should_Return200Repeatedly_When_GettingSameProductManyTimes()
    {
        for (var i = 0; i < Repetitions; i++)
        {
            var response = await _fixture.Client.SendJsonAsync(
                HttpMethod.Get, $"/api/v1/catalog/products/{_fixture.ProductSlug}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Should_Return200Repeatedly_When_GettingCategoriesManyTimes()
    {
        for (var i = 0; i < Repetitions; i++)
        {
            var response = await _fixture.Client.SendJsonAsync(HttpMethod.Get, "/api/v1/catalog/categories");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
