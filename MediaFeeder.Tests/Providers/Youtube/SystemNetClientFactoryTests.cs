using Google.Apis.Http;
using Shouldly;

namespace MediaFeeder.Providers.Youtube;

// Verifies bug med-04 (bugs/medium/med-04-google-client-pinned-handler.md)
// SystemNetClientFactory is a process-lifetime singleton that captures the "retry"
// named client's pooled handler EXACTLY ONCE at construction and then returns that
// same instance for every client it creates. The "retry" pool rotates and disposes
// its handlers every 5 minutes (SetHandlerLifetime), so the pinned handler is
// eventually disposed out from under the Google API client, after which every Google
// call throws ObjectDisposedException. A factory that respects pool rotation must
// NOT hand every client the single handler it captured at construction.
public sealed class SystemNetClientFactoryTests
{
    [Fact]
    public void CreateHttpClient_EachClient_GetsItsOwnHandler_NotThePinnedConstructionTimeInstance()
    {
        var pool = new RotatingPoolFactory();

        using var sut = new SystemNetClientFactory(pool);

        using var firstClient = sut.CreateHttpClient(new CreateHttpClientArgs());
        using var secondClient = sut.CreateHttpClient(new CreateHttpClientArgs());

        // A buggy factory returns the one handler it captured at construction to every
        // client it creates, so both clients share the same inner handler instance. The
        // corrected factory re-resolves from the pool per client, so two clients get two
        // distinct handlers (reflecting pool rotation).
        firstClient.MessageHandler.InnerHandler.ShouldNotBeSameAs(secondClient.MessageHandler.InnerHandler);

        // And the pool must actually have been consulted to serve a fresh handler for the
        // clients - not just once at construction.
        pool.HandlerRequests.ShouldBeGreaterThanOrEqualTo(2);
    }

    // Mimics IHttpClientFactory's pooled handler factory: every CreateHandler call hands
    // out a NEW handler instance, which is what "rotation" looks like from the consumer's
    // point of view.
    private sealed class RotatingPoolFactory : System.Net.Http.IHttpMessageHandlerFactory
    {
        public int HandlerRequests { get; private set; }

        public System.Net.Http.HttpMessageHandler CreateHandler(string name)
        {
            HandlerRequests++;
            return new StubHandler();
        }
    }

    private sealed class StubHandler : System.Net.Http.HttpMessageHandler
    {
        protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request,
            System.Threading.CancellationToken cancellationToken
        )
        {
            var response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK);
            return System.Threading.Tasks.Task.FromResult(response);
        }
    }
}
