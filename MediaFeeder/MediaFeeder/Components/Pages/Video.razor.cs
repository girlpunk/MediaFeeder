using AntDesign;
using MediaFeeder.Data.db;
using MediaFeeder.PlaybackManager;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Components.Pages;

public sealed partial class Video : IDisposable
{
    [Parameter] public int Id { get; set; }

    [Parameter] public string? Next { get; set; }

    [Inject] public required NavigationManager NavigationManager { get; set; }

    [Inject] public required MessageService MessageService { get; set; }

    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; init; }

    [Inject] public required UserManager<AuthUser> UserManager { get; set; }

    [Inject] public required PlaybackSessionManager SessionManager { get; set; }

    private Data.db.Video? VideoObject { get; set; }
    private IProvider? Provider { get; set; }
    private PlaybackSession? PlaybackSession { get; set; }

    private int UpNextCount { get; set; } = 0;
    private TimeSpan UpNextDuration { get; set; } = TimeSpan.Zero;
    private TimeSpan TotalDuration { get; set; } = TimeSpan.Zero;

    protected override async Task OnParametersSetAsync()
    {
        var auth = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(auth.User);

        ArgumentNullException.ThrowIfNull(user);

        if (PlaybackSession == null)
        {
            PlaybackSession = SessionManager.NewSession(user);
            PlaybackSession.SkipEvent += async () => await InvokeAsync(() => GoNext(false));
            PlaybackSession.WatchEvent += async () => await InvokeAsync(() => GoNext(true));
        }

        VideoObject = await Context.Videos.SingleAsync(v => v.Id == Id && v.Subscription.UserId == user.Id);
        StateHasChanged();

        await Context.Entry(VideoObject).Reference(static v => v.Subscription).LoadAsync();
        await Context.Entry(VideoObject.Subscription).Reference(static v => v.ParentFolder).LoadAsync();

        Provider = ServiceProvider.GetServices<IProvider>()
            .Single(provider => provider.ProviderIdentifier == VideoObject.Subscription.Provider);

        if (!string.IsNullOrWhiteSpace(Next))
        {
            var more = Next.Split(',').Select(int.Parse).ToList();
            UpNextCount = more.Count;
            UpNextDuration = TimeSpan.FromSeconds(Context.Videos.Where(v => more.Contains(v.Id)).Sum(static v => v.Duration));
        }
        else
        {
            UpNextCount = 0;
            UpNextDuration = TimeSpan.Zero;
        }

        PlaybackSession.Video = VideoObject;
        PlaybackSession.Provider = Provider.Provider;
        PlaybackSession.UpdateEvent += UpdateTimestamp;

        UpdateTimestamp();
        StateHasChanged();
    }

    private void UpdateTimestamp()
    {
        var remaining = VideoObject?.DurationSpan - PlaybackSession?.CurrentPosition;
        TotalDuration = UpNextDuration + (remaining ?? TimeSpan.Zero);

        InvokeAsync(StateHasChanged);
    }

    private async Task MarkWatched()
    {
        ArgumentNullException.ThrowIfNull(VideoObject);
        Console.WriteLine("Marking as watched");

        VideoObject.Watched = !VideoObject.Watched;
        await Context.SaveChangesAsync();

        StateHasChanged();

        GoNext();
    }

    public async Task GoNext(bool watch)
    {
        if (watch)
            await MarkWatched();

        GoNext();
    }

    public void GoNext()
    {
        if (string.IsNullOrWhiteSpace(Next))
        {
            Console.WriteLine("No next video to try");
            return;
        }

        Console.WriteLine("Going to next video");

        var more = Next.Split(",");

        NavigationManager.NavigateTo($"/video/{more[0]}/{string.Join(',', more[1..])}");
    }

    private async Task Download()
    {
        await MessageService.Info("Not Implemented");
    }

    public void Dispose()
    {
        PlaybackSession?.Dispose();
    }
}
