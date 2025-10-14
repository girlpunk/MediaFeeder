using BlazorComponentUtilities;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Components.Tree;

public sealed partial class TreeSubscription
{
    private int Unwatched { get; set; }
    private int Downloaded { get; set; }

    [Inject]
    public required IDbContextFactory<MediaFeederDataContext> ContextFactory { get; set; }

    [Parameter]
    [EditorRequired]
    public TreeFolder? Parent { get; set; }

    [Parameter]
    [EditorRequired]
    public Subscription? Subscription { get; set; }

    [CascadingParameter(Name = nameof(UnwatchedCache))]
    public Dictionary<int, (int unwatched, int downloaded)>? UnwatchedCache { get; set; }

    [CascadingParameter(Name = nameof(TreeView.SelectedSubscription))]
    public int? SelectedSubscription { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if ((firstRender ||
             Unwatched == 0) &&
            Subscription != null &&
            UnwatchedCache != null &&
            Parent != null
           )
        {
            if (UnwatchedCache.TryGetValue(Subscription.Id, out var value))
            {
                Unwatched = value.unwatched;
                Downloaded = value.downloaded;
            }
            else
            {
                await using var context = await ContextFactory.CreateDbContextAsync();

                Unwatched = await context.Videos
                    .AsNoTracking()
                    .TagWith("TreeView Unwatched")
                    .CountAsync(v => !v.Watched && v.SubscriptionId == Subscription.Id);

                Downloaded = await context.Videos
                    .AsNoTracking()
                    .TagWith("TreeView Downloaded")
                    .CountAsync(v => v.IsDownloaded && v.SubscriptionId == Subscription.Id);
            }

            Parent.AddUnwatched(Unwatched, Downloaded);
            StateHasChanged();
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private bool Selected => SelectedSubscription == Subscription?.Id;

    private string ContainerClasses => new CssBuilder("ant-tree-node-content-wrapper")
        .AddClass("ant-tree-node-selected", Selected)
        .Build();
}
