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

    [Inject] private NavigationManager? NavigationManager { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        if (
            Unwatched == 0 &&
            Subscription != null &&
            UnwatchedCache != null &&
            Parent != null
        )
        {
            await using var context = await ContextFactory.CreateDbContextAsync();

            Unwatched = await context.Videos
                .TagWith("TreeView Unwatched")
                .CountAsync(v => !v.Watched && v.SubscriptionId == Subscription.Id);

            Downloaded = await context.Videos
                .TagWith("TreeView Downloaded")
                .CountAsync(v => v.IsDownloaded && v.SubscriptionId == Subscription.Id);

            Parent.AddUnwatched(Unwatched, Downloaded);
            StateHasChanged();
        }

        await base.OnParametersSetAsync();
    }

    private void OnSelectedChanged(bool obj)
    {
        if (obj && NavigationManager != null && Subscription != null)
            NavigationManager.NavigateTo("subscription/" + Subscription.Id);
    }
}