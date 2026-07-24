namespace MediaFeeder.Components;

using Helpers;
using AntDesign;
using Data;
using Data.db;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;

public sealed partial class VideoCard : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public Video? Video { get; set; }

    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    [Parameter]
    public bool OnClickPreventDefault { get; set; }

    [Inject]
    public required IDbContextFactory<MediaFeederDataContext> ContextFactory { get; set; }

    [Inject]
    public required IMessageService MessageService { get; set; }

    [Inject]
    public required ITimeTickerManager<TimeTickerEntity> TimeTicker { get; set; }

    [Inject]
    public required IServiceProvider ServiceProvider { get; set; }

    [Inject]
    public required HttpContext HttpContext { get; set; }

    [Inject]
    public required ILogger<VideoCard> Logger { get; set; }

    private string? Badge
    {
        get
        {
            if (Video == null)
                return null;

            if (Video.New)
                return "New";

            if (Video.DownloadedPath != null)
                return "Downloaded";

            if (Video.Watched)
                return "Watched";

            return null;
        }
    }

    private string BadgeColour
    {
        get
        {
            if (Video == null)
                return "#000000";

            if (Video.New)
                return "#007bff";

            if (Video.DownloadedPath != null)
                return "#59b352";

            if (Video.Watched)
                return "#444";

            return "#000000";
        }
    }

    // TODO(fae) move to Video class?
    private async Task ToggleStar()
    {
        if (Video == null)
            return;

        await using var context = await ContextFactory.CreateDbContextAsync();
        context.Attach(Video);
        Video.Star = !Video.Star;
        Video.StarDate = DateTimeOffset.Now;
        await context.SaveChangesAsync();

        await MessageService.SuccessAsync($"Star {(Video.Star ? "Added" : "Removed")}");
        StateHasChanged();
    }

    private async Task Watch()
    {
        ArgumentNullException.ThrowIfNull(Video);

        await using var context = await ContextFactory.CreateDbContextAsync();
        var video = await context.Videos.FindAsync(Video.Id);

        if (video != null)
        {
            video.Watched = !video.Watched;
            await context.SaveChangesAsync();
            await MessageService.SuccessAsync(
                $"Marked {(video.Watched ? "Watched" : "Unwatched")}"
            );
        }

        Video = video;

        StateHasChanged();
    }

    private async Task Delete()
    {
        ArgumentNullException.ThrowIfNull(Video?.Subscription);

        if (Video.DownloadedPath == null)
        {
            var providers = ServiceProvider
                .GetServices<IProvider>()
                .ToLookup(static p => p.ProviderIdentifier);

            var providerType = providers[Video.Subscription.Provider].SingleOrDefault();

            if (providerType == null)
            {
                await MessageService.ErrorAsync(
                    $"Could not find a provider for {Video.Subscription.Provider}"
                );
                return;
            }

            await TimeTicker.AddDownloadVideo(Video.Id, providerType, Logger, HttpContext.RequestAborted);

            await MessageService.InfoAsync("Sent for download");
        }
        else
        {
            await using var context = await ContextFactory.CreateDbContextAsync();
            var video = await context.Videos.SingleAsync(v => v.Id == Video.Id);
            ArgumentNullException.ThrowIfNull(video.DownloadedPath);

            File.Delete(video.DownloadedPath);
            video.DownloadedPath = null;

            await context.SaveChangesAsync();
            await MessageService.SuccessAsync("Deleted Download");
        }

        StateHasChanged();
    }
}
