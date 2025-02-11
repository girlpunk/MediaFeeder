﻿using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Components;

namespace MediaFeeder.Components.Tree;

public sealed partial class TreeSubscription
{
    private int Unwatched { get; set; }
    private int Downloaded { get; set; }

    [Parameter]
    [EditorRequired]
    public TreeFolder? Parent { get; set; }

    [Parameter]
    [EditorRequired]
    public Subscription? Subscription { get; set; }

    [CascadingParameter(Name = nameof(UnwatchedCache))]
    public Dictionary<int, (int unwatched, int downloaded)>? UnwatchedCache { get; set; }

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
            Unwatched = unwatched.unwatched;
            Downloaded = unwatched.downloaded;
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