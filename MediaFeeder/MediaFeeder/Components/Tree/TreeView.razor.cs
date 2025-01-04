using MediaFeeder.Data;
using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Components.Tree;

public sealed partial class TreeView
{
    [Inject] public IDbContextFactory<MediaFeederDataContext>? ContextFactory { get; set; }

    public Dictionary<int, int>? UnwatchedCache { get; set; }

    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; init; }

    [Inject] public required UserManager<AuthUser> UserManager { get; set; }

    private List<Folder>? Folders { get; set; }

    private SemaphoreSlim Updating { get; } = new(1);

    protected override async Task OnParametersSetAsync()
    {
        if (UnwatchedCache == null && ContextFactory != null && Folders == null)
            try
            {
                await Updating.WaitAsync();

                var auth = await AuthenticationStateProvider.GetAuthenticationStateAsync();
                var user = await UserManager.GetUserAsync(auth.User);

                ArgumentNullException.ThrowIfNull(user);

                await using var context = await ContextFactory.CreateDbContextAsync();
                UnwatchedCache = context.Videos
                    .Where(v => !v.Watched && v.Subscription.UserId == user.Id)
                    .GroupBy(static v => v.SubscriptionId)
                    .Select(static g => new { Id = g.Key, Count = g.Count() })
                    .ToDictionary(static g => g.Id, static g => g.Count);

                Folders = context.Folders.Where(f => f.UserId == user.Id)
                    .Include(static f => f.Subfolders)
                    .Include(static f => f.Subscriptions)
                    .ToList();
            }
            finally
            {
                Updating.Release();
            }

        await base.OnParametersSetAsync();
    }
}