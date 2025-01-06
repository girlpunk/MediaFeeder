using System.Text.Json;
using Google.Apis.YouTube.v3.Data;
using Microsoft.AspNetCore.StaticFiles;

namespace MediaFeeder.Providers.Youtube;

public sealed class Utils(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory)
{
    internal async Task<string> LoadResourceThumbnail(string itemId, string type, ThumbnailDetails resource, ILogger logger, CancellationToken cancellationToken)
    {
        if (resource?.Maxres?.Url == null)
        {
            logger.LogError("Could not finx maxres thumbnail: {}", JsonSerializer.Serialize(resource));
            return "";
        }

        return await LoadUrlThumbnail(itemId, type, resource.Maxres.Url, logger, cancellationToken);
    }

    internal async Task<string> LoadUrlThumbnail(string itemId, string type, string url, ILogger logger, CancellationToken cancellationToken)
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

            var path = configuration.GetValue<string>("MediaRoot");
            path = Path.Join(path, "thumbnails", type, fileName);

            await using var file = File.OpenWrite(path);
            await request.Content.CopyToAsync(file, cancellationToken);

            return path;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while downloading channel thumbnail");
            return url;
        }
    }
}
