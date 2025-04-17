using System.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace MediaFeeder;

public class AppJwtBearerEvents : JwtBearerEvents
{
    private readonly ILogger<AppJwtBearerEvents> _logger;
 
    public AppJwtBearerEvents(ILogger<AppJwtBearerEvents> logger)
    {
        _logger = logger;
    }

    public override Task AuthenticationFailed(AuthenticationFailedContext context)
    {
        _logger.LogInformation("Token-Expired...");
        return base.AuthenticationFailed(context);
    }

    public override async Task MessageReceived(MessageReceivedContext context)
    {
        if (!context.Request.Query.TryGetValue("access_token", out var values))
        {
            return;
        }

        if (values.Count > 1)
        {
            _logger.LogInformation("More than one parameter found");
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Fail(
                "Only one 'access_token' query string parameter can be defined. " +
                $"However, {values.Count:N0} were included in the request."
            );

            return;
        }

        var token = values.Single();

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogInformation("Empty parameter");
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Fail(
                "The 'access_token' query string parameter was defined, " +
                "but a value to represent the token was not included."
            );

            return;
        }

        _logger.LogInformation("Token found!");
        context.Token = token;
    }

    public override Task TokenValidated(TokenValidatedContext context)
    {
        _logger.LogInformation("TokenValidated");
        context.Success();
        return base.TokenValidated(context);
    }
}