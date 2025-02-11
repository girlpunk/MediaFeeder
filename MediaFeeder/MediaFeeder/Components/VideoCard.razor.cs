using AntDesign;
using MassTransit;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Components;

public sealed partial class VideoCard : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public Video? Video { get; set; }

    [Inject] public required IDbContextFactory<MediaFeederDataContext> ContextFactory { get; set; }

    [Inject] public required IMessageService MessageService { get; set; }

    [Inject] public required IBus Bus { get; set; }
    [Inject] public required IServiceProvider ServiceProvider { get; set; }

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

    private string? BadgeColour
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

    private async Task Watch()
    {
        ArgumentNullException.ThrowIfNull(Video);

        await using var context = await ContextFactory.CreateDbContextAsync();
        var video = await context.Videos.FindAsync(Video.Id);

        if (video != null)
        {
            video.Watched = !video.Watched;
            await context.SaveChangesAsync();
            await MessageService.Success($"Marked {(video.Watched ? "Watched" : "Unwatched")}");
        }

        StateHasChanged();
    }

    private async Task Delete()
    {
        ArgumentNullException.ThrowIfNull(Video?.Subscription);

        if (Video.DownloadedPath != null)
        {
            var providers = ServiceProvider.GetServices<IProvider>()
                .ToLookup(static p => p.ProviderIdentifier);

            var providerType = providers[Video.Subscription.Provider].SingleOrDefault()?.GetType();

            if (providerType == null)
            {
                await MessageService.Error($"Could not find a provider for {Video.Subscription.Provider}");
                return;
            }

            var contractType = typeof(DownloadVideoContract<>).MakeGenericType(providerType);
            var contract = Activator.CreateInstance(contractType, new object[] { Video.Id });
            ArgumentNullException.ThrowIfNull(contract);

            await Bus.Publish(contract);
            await MessageService.Info("Sent for download");
        }
        else
        {
            await using var context = await ContextFactory.CreateDbContextAsync();
            var video = await context.Videos.SingleAsync(v => v.Id == Video.Id);
            ArgumentNullException.ThrowIfNull(video.DownloadedPath);

            File.Delete(video.DownloadedPath);
            video.DownloadedPath = null;

            await context.SaveChangesAsync();
            await MessageService.Success("Deleted Download");
        }

        StateHasChanged();
    }
}