using AntDesign;
using FluentValidation;
using HtmlAgilityPack;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Helpers;
using MediaFeeder.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Components.Dialogs;

public partial class AddSubscription
{
    [Inject] public required MediaFeederDataContext Context { get; set; }
    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; init; }
    [Inject] public required UserManager<AuthUser> UserManager { get; set; }
    public required Form<AddForm> Form { get; set; }
    private AddForm Add { get; set; } = new AddForm();
    private SubscriptionForm Subscription { get; set; } = new SubscriptionForm();

    private IProvider? FoundProvider { get; set; }
    private IList<IProvider>? FoundProviders { get; set; }

    private IEnumerable<(string ProviderIdentifier, string Name)> ProviderNames =>
        Providers.Select(static provider => (provider.ProviderIdentifier, provider.Name));

    private int ActiveStep { get; set; }
    private HtmlDocument? UrlDocument { get; set; }
    private HttpResponseMessage? UrlRequest { get; set; }
    private List<Folder> ExistingFolders { get; set; } = [];
    private HttpClient? HttpClient { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var auth = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(auth.User);

        ArgumentNullException.ThrowIfNull(user);

        Add = new AddForm();
        Subscription = new SubscriptionForm();
        HttpClient?.Dispose();
        HttpClient = HttpClientFactory.CreateClient("retry");

        await base.OnInitializedAsync();
    }

    private async Task CheckUrl(EditContext editContext)
    {
        ArgumentNullException.ThrowIfNull(HttpClient);

        ActiveStep = 1;

        // Download Page
        HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, Add.Url);
        // this agent seems to result in less 302s / not the wanted page.
        req.Headers.UserAgent.ParseAdd("curl/8.14.1");
        UrlRequest = await HttpClient.SendAsync(req);
        if (!UrlRequest.IsSuccessStatusCode)
        {
            //TODO: Do we need to handle differently?
            UrlRequest.EnsureSuccessStatusCode();
        }

        // Parse page if HTML
        if (UrlRequest.Content.Headers.ContentType?.MediaType?.Contains("html") ?? false)
        {
            UrlDocument = new HtmlDocument();
            UrlDocument.LoadHtml(await UrlRequest.Content.ReadAsStringAsync());
        }

        // Offer page to all providers
        var providerMatches = Providers
            .Select(async provider =>
            {
                try
                {
                    Logger.LogDebug("Offering URL to provider {}", provider.Name);
                    return (provider, match: await provider.IsUrlValid(new Uri(Add.Url), UrlRequest, UrlDocument));
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Error while offering URL to provider");
                    return (provider, match: false);
                }
            })
            .ToList();
        await Task.WhenAll(providerMatches);
        var matches = providerMatches
            .Where(static m => m.Result.match)
            .Select(static m => m.Result.provider)
            .ToList();

        // Check if exactly one provider accepted
        if (matches.Count == 1)
        {
            FoundProvider = matches.Single();
            await ChooseProvider();
        }
        // Check if any provider accepted
        else if (matches.Count != 0)
        {
            FoundProvider = null;
            FoundProviders = matches;
            ActiveStep = 1;
            StateHasChanged();
        }
        else
        {
            FoundProvider = null;
            StateHasChanged();
        }
    }

    private async Task ChooseProvider()
    {
        ArgumentNullException.ThrowIfNull(FoundProvider);
        ArgumentNullException.ThrowIfNull(UrlRequest);

        var auth = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(auth.User);

        ExistingFolders = await Context.Folders
            .Where(f => f.User == user)
            .Include(static f => f.Subfolders)
            .Select(Folder.GetProjection(5))
            .Where(static f => f.ParentId == null)
            .ToListAsync();

        await FoundProvider.CreateSubscription(new Uri(Add.Url), UrlRequest, UrlDocument, Subscription);
        ActiveStep = 2;
        StateHasChanged();
    }

    /// <summary>
    /// when form is submitted, close the modal
    /// </summary>
    private async Task OnFinish(EditContext editContext)
    {
        ArgumentNullException.ThrowIfNull(Add);
        ArgumentNullException.ThrowIfNull(FoundProvider);

        var auth = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(auth.User);

        ArgumentNullException.ThrowIfNull(user);

        var subscription = new Subscription
        {
            AutoDownload = Subscription.AutoDownload,
            AutomaticallyDeleteWatched = Subscription.AutomaticallyDeleteWatched,
            ChannelId = Subscription.ChannelId,
            ChannelName = Subscription.ChannelName,
            DownloadLimit = Subscription.DownloadLimit,
            DownloadOrder = Subscription.DownloadOrder,
            Name = Subscription.Name,
            ParentFolderId = Subscription.ParentFolderId,
            PlaylistId = Subscription.PlaylistId,
            Provider = Subscription.Provider ?? throw new InvalidOperationException(),
            RewritePlaylistIndices = Subscription.RewritePlaylistIndices,
            Description = "",
            UserId = user.Id,
        };

        Context.Subscriptions.Add(subscription);

        await Context.SaveChangesAsync();

        var contractType = typeof(SynchroniseSubscriptionContract<>).MakeGenericType(FoundProvider.GetType());
        var contract = Activator.CreateInstance(contractType, subscription.Id);
        ArgumentNullException.ThrowIfNull(contract);

        await Bus.PublishWithGuid(contract);

        await FeedbackRef.CloseAsync();
    }

    public class AddValidator : AbstractValidator<AddForm>
    {
        public AddValidator()
        {
            RuleFor(static f => f.Url).NotEmpty();
        }
    }

    public class SubscriptionValidator : AbstractValidator<SubscriptionForm>
    {
        public SubscriptionValidator()
        {
            RuleFor(static f => f.Name).NotEmpty();
            RuleFor(static f => f.ParentFolderId).NotEqual(0);
            RuleFor(static f => f.PlaylistId).NotEmpty();
            RuleFor(static f => f.ChannelId).NotEmpty();
            RuleFor(static f => f.ChannelName).NotEmpty();
            RuleFor(static f => f.Provider).NotEmpty();
        }
    }

    public class AddForm
    {
        internal string Url { get; set; } = "";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UrlRequest?.Dispose();
            HttpClient?.Dispose();
        }

        base.Dispose(disposing);
    }
}
