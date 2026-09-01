using System.Net;
using System.Text;
using Jellyfin.Plugin.SubsRo.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.SubsRo.Tests.Api;

internal sealed class StubHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;
    public HttpRequestMessage? LastRequest { get; private set; }

    public StubHandler(HttpStatusCode status, string body) => (_status, _body) = (status, body);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;
        return Task.FromResult(new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json")
        });
    }
}

public class SubsRoApiClientTests
{
    private const string SearchBody = """
    {"status":200,"count":1,"items":[{"id":130042,"title":"Obsession","year":2025,
    "imdbid":"tt37287335","tmdbid":"1339713","language":"ro","type":"movie",
    "translator":"MEOO Team","description":"pentru WEB-DL",
    "downloadLink":"https://subs.ro/api/v1.0/subtitle/130042/download"}]}
    """;

    private static SubsRoApiClient Build(StubHandler handler)
        => new(new HttpClient(handler), NullLogger<SubsRoApiClient>.Instance);

    [Fact]
    public async Task SearchAsync_ParsesItems()
    {
        var client = Build(new StubHandler(HttpStatusCode.OK, SearchBody));

        var items = await client.SearchAsync("imdbid", "tt37287335", "key", CancellationToken.None);

        Assert.Single(items);
        Assert.Equal(130042, items[0].Id);
        Assert.Equal("movie", items[0].Type);
        Assert.Equal("https://subs.ro/api/v1.0/subtitle/130042/download", items[0].DownloadLink);
    }

    [Fact]
    public async Task SearchAsync_SendsApiKeyHeaderAndRomanianLanguage()
    {
        var handler = new StubHandler(HttpStatusCode.OK, SearchBody);

        await Build(handler).SearchAsync("imdbid", "tt1", "secret", CancellationToken.None);

        Assert.Equal("secret", handler.LastRequest!.Headers.GetValues("X-Subs-Api-Key").Single());
        Assert.Contains("language=ro", handler.LastRequest.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task SearchAsync_ErrorStatus_ReturnsEmptyWithoutThrowing(HttpStatusCode status)
    {
        // Body contains items to prove the status check (not lack of items) causes empty result
        const string bodyWithItems = """
        {"status":200,"count":1,"items":[{"id":1,"type":"movie","downloadLink":"https://example.com"}]}
        """;
        var client = Build(new StubHandler(status, bodyWithItems));

        var items = await client.SearchAsync("imdbid", "tt1", "key", CancellationToken.None);

        Assert.Empty(items);
    }

    [Fact]
    public async Task SearchAsync_MalformedJson_ReturnsEmptyWithoutThrowing()
    {
        var client = Build(new StubHandler(HttpStatusCode.OK, "not json at all"));

        Assert.Empty(await client.SearchAsync("imdbid", "tt1", "key", CancellationToken.None));
    }

    [Fact]
    public async Task GetQuotaAsync_ParsesRemaining()
    {
        const string body = """
        {"status":200,"quota":{"total_quota":300,"used_quota":12,"remaining_quota":288}}
        """;
        var client = Build(new StubHandler(HttpStatusCode.OK, body));

        var quota = await client.GetQuotaAsync("key", CancellationToken.None);

        Assert.Equal(288, quota!.Remaining);
    }

    [Fact]
    public async Task DownloadAsync_MalformedUri_ReturnsNullWithoutThrowing()
    {
        var client = Build(new StubHandler(HttpStatusCode.OK, "data"));

        // Malformed URI should not throw
        var data = await client.DownloadAsync("http://", "key", CancellationToken.None);

        Assert.Null(data);
    }

    [Fact]
    public async Task DownloadAsync_ApiKeyWithNewline_ReturnsNullWithoutThrowing()
    {
        var client = Build(new StubHandler(HttpStatusCode.OK, "data"));

        // API key with newline should not throw
        var data = await client.DownloadAsync("https://subs.ro/api/v1.0/subtitle/1/download", "key\n", CancellationToken.None);

        Assert.Null(data);
    }
}
