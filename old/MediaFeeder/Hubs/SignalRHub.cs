using MediaFeeder.Services;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace MediaFeeder.Hubs;

public class SignalRHub : Hub
{

    private static readonly ConcurrentDictionary<string, bool> _onlineUsers = new ConcurrentDictionary<string, bool>();
    private readonly CurrentUserService _currentUserService;

    public SignalRHub(
        CurrentUserService currentUserService
    )
    {
        _currentUserService = currentUserService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId is not null)
        {
            await UpdateOnlineUsers();
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var userId = _currentUserService.UserId;
        if (userId is not null)
        {
            //try to remove key from dictionary
            if (!_onlineUsers.TryRemove(userId, out _))
            {
                //if not possible to remove key from dictionary, then try to mark key as not existing in cache
                _onlineUsers.TryUpdate(userId, false, true);
            }

            await UpdateOnlineUsers();
        }

        await base.OnDisconnectedAsync(exception);
    }

    private Task UpdateOnlineUsers()
    {
        var count = GetOnlineUsersCount();
        return Clients.All.SendAsync("UpdateOnlineUsers", count);
    }

    public static int GetOnlineUsersCount()
    {
        return _onlineUsers.Count(p => p.Value);
    }
}