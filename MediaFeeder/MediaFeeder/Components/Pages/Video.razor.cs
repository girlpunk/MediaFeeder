using AntDesign;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Components.Pages;

public sealed partial class Video
{
    [Parameter] public int Id { get; set; }

    [Parameter] public string? Next { get; set; }

    [Inject] public required NavigationManager NavigationManager { get; set; }

    [Inject] public required MessageService MessageService { get; set; }

    private Data.db.Video? VideoObject { get; set; }
    private IProvider? Provider { get; set; }

    private int UpNextCount { get; set; }
    private TimeSpan UpNextDuration { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        VideoObject = await Context.Videos.SingleAsync(v => v.Id == Id);
        Provider = null;
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

        StateHasChanged();
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
}
