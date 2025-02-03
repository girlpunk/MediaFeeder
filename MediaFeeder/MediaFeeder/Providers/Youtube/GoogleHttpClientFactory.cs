using Google.Apis.Http;

namespace MediaFeeder.Providers.Youtube;

// From https://github.com/googleapis/google-api-dotnet-client/issues/1652#issuecomment-696112715
internal sealed class SystemNetClientFactory(IHttpMessageHandlerFactory factory) : HttpClientFactory
{
    private readonly HttpMessageHandler _handler = factory.CreateHandler("retry");

    protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args) => _handler;
}
