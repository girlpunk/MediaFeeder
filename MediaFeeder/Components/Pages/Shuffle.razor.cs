using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;

namespace MediaFeeder.Components.Pages;

public sealed partial class Shuffle
{
    [Parameter] public int? FolderId { get; set; }

    [Parameter] public int? SubscriptionId { get; set; }

    [Inject] public MediaFeederDataContext? DataContext { get; set; }
    [Inject] public NavigationManager? NavigationManager { get; set; }
    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; init; }
    [Inject] public required UserManager<AuthUser> UserManager { get; set; }

    protected override async Task OnInitializedAsync()
    {
        ArgumentNullException.ThrowIfNull(DataContext);
        ArgumentNullException.ThrowIfNull(NavigationManager);

        var auth = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(auth.User);
        ArgumentNullException.ThrowIfNull(user);

        var videos = await ShuffleHelper.Shuffle(
            DataContext,
            user,
            60,
            FolderId,
            SubscriptionId);

        var first = videos[0];
        videos.RemoveAt(0);

        var url = $"video/{first.Id}/{string.Join(',', videos.Select(static v => v.Id))}";
        NavigationManager.NavigateTo(url);
    }
}
