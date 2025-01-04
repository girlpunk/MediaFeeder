using MediaFeeder.Data;
using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Components.Pages;

public sealed partial class Shuffle
{
    [Parameter] public int? FolderId { get; set; }

    [Parameter] public int? SubscriptionId { get; set; }

    [Inject] public MediaFeederDataContext? DataContext { get; set; }
    [Inject] public NavigationManager? NavigationManager { get; set; }

    private TimeSpan _timeRemaining = TimeSpan.FromHours(1);
    private readonly Queue<MediaFeeder.Data.db.Video> _videos = new();
    private List<Subscription> _subscriptions = [];

    protected override async Task OnInitializedAsync()
    {
        ArgumentNullException.ThrowIfNull(DataContext);
        ArgumentNullException.ThrowIfNull(NavigationManager);

        if (FolderId != null)
            _subscriptions = await DataContext.Subscriptions
                .Where(s => s.ParentFolderId == FolderId)
                .OrderBy(static s => Guid.NewGuid())
                .ToListAsync();
        else if (SubscriptionId != null)
            _subscriptions = [await DataContext.Subscriptions.SingleAsync(s => s.Id == SubscriptionId)];
        else
            _subscriptions = await DataContext.Subscriptions
                .OrderBy(static s => Guid.NewGuid())
                .ToListAsync();

        StateHasChanged();

        _videos.Enqueue(
            await DataContext.Videos
                .Where(v => v.Watched == false && _subscriptions.Contains(v.Subscription))
                .OrderBy(static v => v.PublishDate)
                .FirstAsync()
        );

        _timeRemaining -= _videos.Peek().DurationSpan;

        StateHasChanged();

        var idleLoops = 0;

        while (idleLoops <= 2)
        {
            var addedVideo = false;

            foreach (var subscription in _subscriptions)
            {
                var video = await DataContext.Videos
                    .Where(v => v.Subscription == subscription && v.Watched == false && !_videos.Select(static q => q.Id).Contains(v.Id))
                    .OrderBy(static v => v.PublishDate)
                    .FirstOrDefaultAsync();

                if (video == null || video.DurationSpan > _timeRemaining)
                    continue;

                _videos.Enqueue(video);
                _timeRemaining -= video.DurationSpan;
                addedVideo = true;

                StateHasChanged();
            }

            if (!addedVideo)
                idleLoops++;
        }

        var url = $"/video/{_videos.Dequeue().Id}/{string.Join(',', _videos.Select(static v => v.Id))}";

        NavigationManager.NavigateTo(url);
    }
}
