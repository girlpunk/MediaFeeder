using Google.Apis.YouTube.v3.Data;
using Microsoft.AspNetCore.StaticFiles;

namespace MediaFeeder.Providers.Youtube;

public sealed class Utils
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public Utils(
        IConfiguration configuration,
        [FromKeyedServices("retry")] IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    internal async Task<string?> LoadResourceThumbnail(string itemId, string type, ThumbnailDetails resource, ILogger logger, CancellationToken cancellationToken) =>
        await LoadUrlThumbnail(itemId, type, resource.Maxres.Url, logger, cancellationToken);

    internal async Task<string?> LoadUrlThumbnail(string itemId, string type, string url, ILogger logger, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("retry");

        var request = await httpClient.GetAsync(url, cancellationToken);
        request.EnsureSuccessStatusCode();

        var ext = new FileExtensionContentTypeProvider().Mappings.SingleOrDefault(g => g.Value == request.Headers.GetValues("Content-Type").First()).Key;
        var fileName = $"{itemId}{ext}";

        var path = _configuration.GetValue<string>("MediaRoot");
        path = Path.Join(path, "thumbnails", type, fileName);

        await using var file = File.OpenWrite(path);
        await request.Content.CopyToAsync(file, cancellationToken);

        return path;
    }
}
