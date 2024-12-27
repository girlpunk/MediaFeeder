using AntDesign;
using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Components;

namespace MediaFeeder.Components.Pages;

public partial class Video
{
    [Parameter] public int Id { get; set; }

    [Parameter] public string? Next { get; set; }
    
    [Inject] public required NavigationManager NavigationManager { get; set; }
    
    [Inject] public required MessageService MessageService { get; set; }

    private YtManagerAppVideo? VideoObject { get; set; }
    private Type? Frame { get; set; }
    private string WatchButtonText => VideoObject?.Watched ?? false ? "Mark as not watched" : "Mark as watched";

    private string WatchButtonIcon =>
        VideoObject?.Watched ?? false ? IconType.Outline.EyeInvisible : IconType.Outline.Eye;

    private Color WatchButtonColour => VideoObject?.Watched ?? false ? Color.Green10 : Color.Blue1;
    private string DownloadButtonText => VideoObject?.DownloadedPath == null ? "Download" : "Delete Download";

    private string DownloadButtonIcon =>
        VideoObject?.DownloadedPath == null ? IconType.Outline.Download : IconType.Outline.Delete;

    private Color DownloadButtonColour => VideoObject?.DownloadedPath == null ? Color.Blue1 : Color.Green10;
    private int UpNextCount { get; set; }
    private TimeSpan UpNextDuration { get; set; }

    protected override async Task OnInitializedAsync()
    {
        VideoObject = Context.YtManagerAppVideos.Single(v => v.Id == Id);
        await Context.Entry(VideoObject).Reference(v => v.Subscription).LoadAsync();
        Frame = ServiceProvider.GetServices<IProvider>()
            .Single(provider => provider.ProviderIdentifier == VideoObject.Subscription.Provider)
            .VideoFrameView;

        if (!string.IsNullOrWhiteSpace(Next))
        {
            var more = Next.Split(',').Select(int.Parse).ToList();
            UpNextCount = more.Count;
            UpNextDuration = TimeSpan.FromSeconds(Context.YtManagerAppVideos.Where(v => more.Contains(v.Id)).Sum(v => v.Duration));
        }
    }

    private async Task MarkWatched()
    {
        ArgumentNullException.ThrowIfNull(VideoObject);
        
        VideoObject.Watched = !VideoObject.Watched;
        await Context.SaveChangesAsync();

        GoNext();
    }

    private void GoNext()
    {
        if (string.IsNullOrWhiteSpace(Next))
            return;
        
        var more = Next.Split(",");

        NavigationManager.NavigateTo($"/video/{more[0]}/{string.Join(',', more[1..])}");
    }

    private async Task Download()
    {
        await MessageService.Info("Not Implemented");
    }
}
