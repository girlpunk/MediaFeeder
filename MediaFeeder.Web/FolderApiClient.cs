using System.Net.Http.Json;
using MediaFeeder.DTOs.DTOs;

namespace MediaFeeder.Web;

public class FolderApiClient(HttpClient httpClient)
{
    public async Task<IList<int>> Get() =>
        await httpClient.GetFromJsonAsync<List<int>>("/api/folders").ConfigureAwait(false) ?? throw new InvalidOperationException();

    public async Task<FolderGet> Get(int id) =>
        await httpClient.GetFromJsonAsync<FolderGet>($"/api/folders/{id}").ConfigureAwait(false) ?? throw new InvalidOperationException();
}
