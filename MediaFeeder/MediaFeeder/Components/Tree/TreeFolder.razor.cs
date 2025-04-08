using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Components;

namespace MediaFeeder.Components.Tree;

public sealed partial class TreeFolder
{
    private int Unwatched { get; set; } = 0;
    private int Downloaded { get; set; } = 0;

    [Parameter]
    [EditorRequired]
    public Folder? Folder { get; set; }

    [Parameter] public TreeFolder? Parent { get; set; }

    [Inject] private NavigationManager? NavigationManager { get; set; }

    internal int AddUnwatched(int unwatched, int downloaded)
    {
        Unwatched += unwatched;
        Downloaded += downloaded;
        Parent?.AddUnwatched(unwatched, downloaded);
        StateHasChanged();

        return Unwatched;
    }

    private void OnSelectedChanged(bool arg)
    {
        if (arg && NavigationManager != null && Folder != null)
            NavigationManager.NavigateTo("folder/" + Folder.Id);
    }
}
