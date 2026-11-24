using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace MediaFeeder.Providers.Youtube;

public sealed class UtilsTests : IDisposable
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly DirectoryInfo _mediaRoot = Directory.CreateTempSubdirectory();

    public void Dispose() => _mediaRoot.Delete(true);

    [Fact]
    public async Task LoadUrlThumbnail_SavesDownloadedImageAndReturnsRelativePath()
    {
        Directory.CreateDirectory(Path.Join(_mediaRoot.FullName, "thumbnails", "channels"));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["MediaRoot"] = _mediaRoot.FullName,
            })
            .Build();

        using var handler = new FixedResponseHttpMessageHandler(PngHeader);
        using var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        using var returnedClient = httpClientFactory.CreateClient("retry");
        returnedClient.Returns(httpClient);

        var logger = Substitute.For<ILogger>();
        var utils = new Utils(configuration, httpClientFactory);

        var relativePath = await utils.LoadUrlThumbnail(
            "abc123",
            "channels",
            "https://example.com/thumbnail.png",
            logger,
            CancellationToken.None
        );

        relativePath.ShouldBe(Path.Join("thumbnails", "channels", "abc123.png"));

        var savedFile = Path.Join(_mediaRoot.FullName, "thumbnails", "channels", "abc123.png");
        File.Exists(savedFile).ShouldBeTrue();
        new FileInfo(savedFile).Length.ShouldBe(PngHeader.Length);
    }

    private sealed class FixedResponseHttpMessageHandler : HttpMessageHandler
    {
        private readonly byte[] _imageBytes;

        public FixedResponseHttpMessageHandler(byte[] imageBytes) => _imageBytes = imageBytes;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var content = new ByteArrayContent(_imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = content,
            };

            return Task.FromResult(response);
        }
    }
}
