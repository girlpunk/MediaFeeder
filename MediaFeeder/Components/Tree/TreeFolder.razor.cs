using BlazorComponentUtilities;
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

    [CascadingParameter(Name = nameof(TreeView.SelectedFolder))]
    public int? SelectedFolder { get; set; }

    internal int AddUnwatched(int unwatched, int downloaded)
    {
        Unwatched += unwatched;
        Downloaded += downloaded;
        Parent?.AddUnwatched(unwatched, downloaded);
        StateHasChanged();

        return Unwatched;
    }

    private bool Selected => SelectedFolder == Folder?.Id;

    private string ContainerClasses => new CssBuilder("ant-tree-node-content-wrapper")
        .AddClass("ant-tree-node-selected", Selected)
        .Build();
}
