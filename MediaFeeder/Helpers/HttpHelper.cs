using Humanizer;

namespace MediaFeeder.Helpers;

public static class HttpHelper
{
    public static async Task EnsureContentTypeHeader(HttpResponseMessage request, string contentType)
    {
        if (string.Equals(request.Content.Headers.ContentType?.MediaType, contentType,
                StringComparison.OrdinalIgnoreCase))
            return;

        var body = await request.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"Response Content-Type is not {contentType}: [{request.Content.Headers.ContentType}] {body.Truncate(100)}");
    }
}