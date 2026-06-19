using System.Net;
using Ecommerce.SmokeTests.Infrastructure;
using FluentAssertions;

namespace Ecommerce.SmokeTests.Load;

[Collection("Smoke API")]
public sealed class LoadAndLatencyTests
{
    private const int TotalRequests = 50;
    private const int BatchSize = 5;

    private readonly SmokeApiFixture _fixture;

    public LoadAndLatencyTests(SmokeApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Should_GenerateLatencyDataPoints_When_SendingBurstsOfRequests()
    {
        for (var batchStart = 0; batchStart < TotalRequests; batchStart += BatchSize)
        {
            var batch = Enumerable.Range(0, BatchSize)
                .Select(i => _fixture.Client.SendJsonAsync(
                    HttpMethod.Get, $"/api/v1/catalog/products?page_number={(batchStart + i) % 3 + 1}&page_size=10"));

            var responses = await Task.WhenAll(batch);

            responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        }
    }
}
