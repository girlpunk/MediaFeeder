using System;
using Google.Apis.Http;

namespace MediaFeeder.Providers.Youtube;

// From https://github.com/googleapis/google-api-dotnet-client/issues/1652#issuecomment-696112715
internal sealed class SystemNetClientFactory(IHttpMessageHandlerFactory factory) : HttpClientFactory, IDisposable
{
    private readonly HttpMessageHandler _handler = factory.CreateHandler("retry");
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }

    protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args) => _handler;

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
