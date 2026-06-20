using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Ecommerce.SmokeTests.Infrastructure;

internal static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> SendJsonAsync(
        this HttpClient client, HttpMethod method, string url, string? accessToken = null) =>
        SendAsync(client, method, url, content: null, accessToken);

    public static Task<HttpResponseMessage> SendJsonAsync<TBody>(
        this HttpClient client, HttpMethod method, string url, TBody body, string? accessToken = null) =>
        SendAsync(client, method, url, JsonContent.Create(body), accessToken);

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string url, HttpContent? content, string? accessToken)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        if (accessToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await client.SendAsync(request);
    }
}
