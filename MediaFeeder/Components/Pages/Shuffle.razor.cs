using MediaFeeder.Data;
using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Components.Pages;

public sealed partial class Shuffle
{
    [Parameter] public int? FolderId { get; set; }

    [Parameter] public int? SubscriptionId { get; set; }

    [Inject] public MediaFeederDataContext? DataContext { get; set; }
    [Inject] public NavigationManager? NavigationManager { get; set; }
    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; init; }
    [Inject] public required UserManager<AuthUser> UserManager { get; set; }

    private TimeSpan _timeRemaining = TimeSpan.FromHours(1);
    private readonly Queue<MediaFeeder.Data.db.Video> _videos = new();
    private List<Subscription> _subscriptions = [];

    protected override async Task OnInitializedAsync()
    {
        ArgumentNullException.ThrowIfNull(DataContext);
        ArgumentNullException.ThrowIfNull(NavigationManager);

        var auth = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(auth.User);

        ArgumentNullException.ThrowIfNull(user);

        if (FolderId != null)
        {
            var folder = await DataContext.Folders
                .Where(f => f.Id == FolderId && f.UserId == user.Id)
                .Select(Folder.GetProjection(5))
                .SingleAsync();
            var subfolderIds = folder.RecursiveFolderIds(5).ToList();

            _subscriptions = await DataContext.Subscriptions
                .Where(s => subfolderIds.Contains(s.ParentFolderId) && s.UserId == user.Id)
                .OrderBy(static _ => EF.Functions.Random())
                .ToListAsync();
        }
        else if (SubscriptionId != null)
            _subscriptions = [await DataContext.Subscriptions.SingleAsync(s => s.Id == SubscriptionId && s.UserId == user.Id)];
        else
            _subscriptions = await DataContext.Subscriptions
                .Where(s => s.UserId == user.Id)
                .OrderBy(static _ => EF.Functions.Random())
                .ToListAsync();

        StateHasChanged();

        _videos.Enqueue(
            await DataContext.Videos
                .Where(v => v.Watched == false && _subscriptions.Select(static s => s.Id).Contains(v.SubscriptionId))
                .OrderBy(static v => v.PublishDate)
                .FirstAsync()
        );

        _timeRemaining -= _videos.Peek().DurationSpan ?? TimeSpan.Zero;

        StateHasChanged();

        var idleLoops = 0;

        while (idleLoops <= 2)
        {
            var addedVideo = false;

            foreach (var subscription in _subscriptions)
            {
                var video = await DataContext.Videos
                    .Where(v => v.SubscriptionId == subscription.Id && v.Watched == false && !_videos.Select(static q => q.Id).Contains(v.Id))
                    .OrderBy(static v => v.PublishDate)
                    .FirstOrDefaultAsync();

                if (video == null || video.DurationSpan > _timeRemaining)
                    continue;

                _videos.Enqueue(video);
                _timeRemaining -= video.DurationSpan ?? TimeSpan.Zero;
                addedVideo = true;

                StateHasChanged();
            }

            if (!addedVideo)
                idleLoops++;
        }

        var url = $"video/{_videos.Dequeue().Id}/{string.Join(',', _videos.Select(static v => v.Id))}";

        NavigationManager.NavigateTo(url);
    }
}
