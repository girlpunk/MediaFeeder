using System.Net.Http.Json;
using MediaFeeder.DTOs.DTOs;

namespace MediaFeeder.Web;

public class SubscriptionApiClient(HttpClient httpClient)
{
    public async Task<SubscriptionGet> Get(int id) =>
        await httpClient.GetFromJsonAsync<SubscriptionGet>($"/api/subscriptions/{id}").ConfigureAwait(false) ?? throw new InvalidOperationException();
}
