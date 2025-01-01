using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Components;

namespace MediaFeeder.Components.Tree;

public sealed partial class TreeSubscription
{
    private int Unwatched { get; set; }

    [Parameter][EditorRequired] public YtManagerAppSubscription? Subscription { get; set; }

    [CascadingParameter(Name = nameof(UnwatchedCache))]
    public Dictionary<int, int>? UnwatchedCache { get; set; }

    [Inject] private NavigationManager? NavigationManager { get; set; }

    protected override void OnParametersSet()
    {
        if (
            Unwatched == 0 &&
            Subscription != null &&
            UnwatchedCache != null &&
            UnwatchedCache.TryGetValue(Subscription.Id, out var unwatched) &&
            Parent != null
        )
        {
            Unwatched = unwatched;
            Parent.AddUnwatched(unwatched);
            StateHasChanged();
        }

        base.OnParametersSet();
    }

    private void OnSelectedChanged(bool obj)
    {
        if (obj && NavigationManager != null && Subscription != null)
            NavigationManager.NavigateTo("/subscription/" + Subscription.Id);
    }
}