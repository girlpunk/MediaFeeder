using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace MediaFeeder.Web;

public class MediaFeederAuthorizationMessageHandler : AuthorizationMessageHandler
{
    public MediaFeederAuthorizationMessageHandler(IAccessTokenProvider provider,
        NavigationManager navigation, IConfiguration config)
        : base(provider, navigation)
    {
        ConfigureHandler(
            authorizedUrls: new[] { config.GetValue<string>("api") ?? string.Empty },
            scopes: config.GetSection("Scopes").Get<List<string>>());
    }
}