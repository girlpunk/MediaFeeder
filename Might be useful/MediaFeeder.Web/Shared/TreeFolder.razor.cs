using MediaFeeder.DTOs.DTOs;
using Microsoft.AspNetCore.Components;

namespace MediaFeeder.Web.Shared;

public partial class TreeFolder
{
    private readonly List<SubscriptionGet> _subscriptions = [];
    [Parameter] public int? Id { get; set; }

    private FolderGet? Folder { get; set; }

    [Inject] public FolderApiClient? FolderApiClient { get; set; }

    [Inject] public SubscriptionApiClient? SubscriptionApiClient { get; set; }

    private int Unwatched { get; set; } = 0;

    [Parameter] public TreeFolder? Parent { get; set; }

    private void AddUnwatched(int add)
    {
        Unwatched += add;
        Parent?.AddUnwatched(add);
    }

    protected override async Task OnParametersSetAsync()
    {
        if (FolderApiClient != null && SubscriptionApiClient != null && Folder == null && Id != null)
        {
            Folder = await FolderApiClient.Get(Id.Value);
            await Task.WhenAll(Folder.ChildSubscriptions.Select(async id =>
            {
                var subscription = await SubscriptionApiClient.Get(id);
                _subscriptions.Add(subscription);
                AddUnwatched(subscription.Unwatched);
            }));
        }
    }
}