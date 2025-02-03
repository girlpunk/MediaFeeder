using System.Text.Json;
using Google.Apis.YouTube.v3.Data;
using Microsoft.AspNetCore.StaticFiles;

namespace MediaFeeder.Providers.Youtube;

public sealed class Utils(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory)
{
    internal async Task<string?> LoadResourceThumbnail(string itemId, string type, ThumbnailDetails resource, ILogger logger, CancellationToken cancellationToken)
    {
        if (resource.Maxres?.Url == null && resource.High?.Url == null)
            logger.LogError("Could not find maxres thumbnail: {}", JsonSerializer.Serialize(resource));

        return await LoadUrlThumbnail(itemId, type, resource.Maxres?.Url ?? resource.High?.Url ?? resource.Medium?.Url ?? resource.Standard.Url, logger, cancellationToken);
    }

    internal async Task<string?> LoadUrlThumbnail(string itemId, string type, string url, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient("retry");

            var request = await httpClient.GetAsync(url, cancellationToken);
            request.EnsureSuccessStatusCode();

            var ext = new FileExtensionContentTypeProvider().Mappings
                .FirstOrDefault(g => g.Value == request.Content.Headers.ContentType?.MediaType)
                .Key ?? ".png";
            var fileName = $"{itemId}{ext}";

            var root = configuration.GetValue<string>("MediaRoot") ?? throw new InvalidOperationException();
            var path = Path.Join(root, "thumbnails", type, fileName);

            await using var file = File.OpenWrite(path);
            await request.Content.CopyToAsync(file, cancellationToken);

            return path.Remove(0, root.Length);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while downloading channel thumbnail for {} from {}", itemId, url);
            return null;
        }
    }
}
