using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace MediaFeeder.Providers.Youtube;

// Verifies bug med-03 (bugs/medium/med-03-relative-vs-absolute-path-mismatch.md)
// Utils.LoadUrlThumbnail stores its result verbatim in Video.Thumb / Subscription.Thumb,
// which the gRPC surface then opens with File.OpenRead(video.Thumb) and NO MediaRoot
// join. So the value returned here must resolve on its own: a path that is only valid
// relative to MediaRoot makes File.OpenRead throw FileNotFoundException on gRPC while
// the REST path (which does Path.Join(MediaRoot, value)) works.
public sealed class UtilsPathConventionTests : IDisposable
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly DirectoryInfo _mediaRoot = Directory.CreateTempSubdirectory();
    private readonly string _testUrl = "https://example.com/thumb.png";

    public void Dispose() => _mediaRoot.Delete(true);

    [Fact]
    public async Task LoadUrlThumbnail_ReturnedPath_ResolvesDirectlyAndIsRooted()
    {
        Directory.CreateDirectory(Path.Join(_mediaRoot.FullName, "thumbnails", "channels"));

        var utils = CreateUtils();

        var returnedPath = await utils.LoadUrlThumbnail(
            "abc123",
            "channels",
            _testUrl,
            Substitute.For<ILogger>(),
            CancellationToken.None
        );

        returnedPath.ShouldNotBeNull();

        // The stored thumb is opened as File.OpenRead(value) with no MediaRoot join on the
        // gRPC surface, so the value must be an absolute path that already resolves to the
        // file on disk. A CWD-relative path is exactly the mismatch this bug is about.
        Path.IsPathRooted(returnedPath).ShouldBeTrue();
        File.Exists(returnedPath).ShouldBeTrue();
    }

    [Fact]
    public async Task LoadUrlThumbnail_ReturnedPath_MatchWhatGrcpSurfaceOpens()
    {
        Directory.CreateDirectory(Path.Join(_mediaRoot.FullName, "thumbnails", "channels"));

        var utils = CreateUtils();

        var returnedPath = await utils.LoadUrlThumbnail(
            "abc123",
            "channels",
            _testUrl,
            Substitute.For<ILogger>(),
            CancellationToken.None
        );

        returnedPath.ShouldNotBeNull();

        // This is precisely the call the gRPC service makes with the stored thumb:
        // File.OpenRead(video.Thumb). Today the relative value makes it throw;
        // the corrected (MediaRoot-joined, absolute) value must open cleanly.
        var ex = await Record.ExceptionAsync(async () =>
        {
            await using var stream = File.OpenRead(returnedPath);
            var buffer = new byte[4];
            (await stream.ReadAsync(buffer)).ShouldBe(4);
        });
        ex.ShouldBeNull();
    }

    private Utils CreateUtils()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["MediaRoot"] = _mediaRoot.FullName,
            })
            .Build();

        var handler = new FixedResponseHttpMessageHandler(PngHeader);
        using var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("retry").Returns(httpClient);

        return new Utils(configuration, httpClientFactory);
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
