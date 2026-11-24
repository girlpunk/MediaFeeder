using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Shouldly;

namespace MediaFeeder.Controllers;

// Verifies bug sec-02 (bugs/security/sec-02-anonymous-full-sync-trigger.md)
// GET /api/Stats/sync enqueues SynchroniseAllConsumer — a full sync of every
// subscription — and is reachable with no authentication at all, an unauthenticated
// DoS / cost-amplification vector. The action must not be [AllowAnonymous].
// (Without TestServer/bunit — unavailable in the fixed package set — the auth
// posture of the endpoint is only observable through its attributes.)
public sealed class StatsControllerSyncAuthorizationTests
{
    [Fact]
    public void Sync_Endpoint_DoesNotAllowAnonymous()
    {
        var method = typeof(StatsController).GetMethod(nameof(StatsController.Sync))!;

        method.GetCustomAttributes<AllowAnonymousAttribute>().ShouldBeEmpty();
    }
}
