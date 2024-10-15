using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

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
        {
            try
            {
                Folders = await ApiClient.Get();
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }
    }
}
