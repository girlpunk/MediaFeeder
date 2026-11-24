using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using System.Net;
using System.Text;

namespace MediaFeeder.Providers.Youtube;

// Verifies bug low-10 (bugs/low/low-10-utils-thumbnail-issues.md):
// 10a - a failed (4xx) request whose content type is image/* is still "accepted": the error
//      response body is written into the thumbnail file and a success path is returned;
// 10b - when no thumbnail URL exists at all, the fallback chain NREs on resource.Standard.Url
//      instead of returning null.
public sealed class UtilsThumbnailEdgeCaseTests : IDisposable
{
    private readonly DirectoryInfo _mediaRoot = Directory.CreateTempSubdirectory();

    public void Dispose() => _mediaRoot.Delete(true);

    [Fact]
    public async Task LoadUrlThumbnail_FailedRequestWithImageContentType_DoesNotStoreErrorBodyAsThumbnail()
    {
        const string errorBody = "<html>Not Found</html>";
        Directory.CreateDirectory(Path.Join(_mediaRoot.FullName, "thumbnails", "video"));

        var utils = CreateUtils(new FixedResponseHandler(HttpStatusCode.NotFound, errorBody, "image/png"));

        await utils.LoadUrlThumbnail(
            "item1",
            "video",
            "https://img.example.com/a.png",
            Substitute.For<ILogger>(),
            CancellationToken.None);

        var savedFile = Path.Join(_mediaRoot.FullName, "thumbnails", "video", "item1.png");

        if (File.Exists(savedFile))
            File.ReadAllText(savedFile).ShouldNotBe(errorBody);
    }

    [Fact]
    public async Task LoadResourceThumbnail_MissingAllUrls_ReturnsNullInsteadOfThrowing()
    {
        var utils = CreateUtils(new FixedResponseHandler(HttpStatusCode.OK, "x", "image/png"));

        await Should.NotThrowAsync(async () =>
        {
            var result = await utils.LoadResourceThumbnail(
                "item1",
                "video",
                new ThumbnailDetails(),
                Substitute.For<ILogger>(),
                CancellationToken.None);

            result.ShouldBeNull();
        });
    }

    private Utils CreateUtils(HttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["MediaRoot"] = _mediaRoot.FullName,
            })
            .Build();

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("retry").Returns(new HttpClient(handler));

        return new Utils(configuration, httpClientFactory);
    }

    private sealed class FixedResponseHandler(HttpStatusCode statusCode, string body, string mediaType) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        });
    }
}
