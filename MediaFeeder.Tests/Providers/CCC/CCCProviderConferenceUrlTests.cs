using System.Net;
using System.Net.Http.Headers;
using MediaFeeder.Data;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace MediaFeeder.Providers.CCC;

// Verifies bug func-01 (bugs/functional/func-01-ccc-conference-wrong-response.md)
// The "pretty" conference URL flow must populate the form from the JSON API response.
// Today the HTML `request` response is passed to CreateConferenceSubscription, whose
// content-type check throws on an HTML response.
public sealed class CCCProviderConferenceUrlTests
{
    private const string ConferenceJson = "{\"title\":\"38C3\",\"slug\":\"38c3\",\"events\":[]}";

    [Fact]
    public async Task CreateSubscription_PrettyConferenceUrl_PopulatesSubscriptionFormFromApiJson()
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        using var client = httpClientFactory.CreateClient("retry");
        client.Returns(new HttpClient(new StaticJsonHttpMessageHandler(ConferenceJson)));

        var provider = new CCCProvider(httpClientFactory, Substitute.For<ILogger<CCCProvider>>());

        // The user entered the standard pretty URL; the `request` response is the HTML page of media.ccc.de.
        using var request = new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://media.ccc.de/c/38c3"),
            Content = new StringContent("<html><body>38C3</body></html>"),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        var form = new SubscriptionForm();

        await Should.NotThrowAsync(() =>
            provider.CreateSubscription(new Uri("https://media.ccc.de/c/38c3"), request, null, form)
        );

        form.Name.ShouldBe("38C3");
        form.ChannelName.ShouldBe("38C3");
        form.Provider.ShouldBe("CCC");
    }

    private sealed class StaticJsonHttpMessageHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var content = new StringContent(json);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = content,
                }
            );
        }
    }
}
