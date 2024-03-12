using Microsoft.AspNetCore.Components;

namespace MediaFeeder.Web.Pages;

public partial class Home
{
    [Inject]
    public FolderApiClient? ApiClient { get; set; }

    private IList<int>? Folders { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (Folders == null && ApiClient != null)
            Folders = await ApiClient.Get();
    }
}
