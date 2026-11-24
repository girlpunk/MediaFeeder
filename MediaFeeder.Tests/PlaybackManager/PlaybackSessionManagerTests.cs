using System.Collections.Concurrent;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace MediaFeeder.PlaybackManager;

// Verifies bug func-03 (bugs/functional/func-03-playback-session-manager-dispose-race.md).
// PlaybackSessionManager keeps live sessions in a plain, unsynchronized Dictionary:
//   3a — Dispose() enumerates PlaybackSessions while each PlaybackSession.Dispose()
//        calls manager.RemoveSession(), mutating the dictionary mid-enumeration. Note:
//        on the .NET 10 runtime a bare Remove of the current entry does not throw
//        (verified with a minimal repro), so the plain dispose tests below assert the
//        end-state contract (no throw, registry emptied) and guard the fixed
//        implementation instead; Dispose_SessionRegisteredViaUpdateEventDuringDisposal
//        detects 3a directly, because the UpdateEvent fired by RemoveSession lets a
//        subscriber run NewSession (a Dictionary Add) while Dispose's enumeration is
//        still in flight, which .NET 10's enumerator rejects with InvalidOperationException
//        ("Collection was modified; enumeration operation may not execute").
//   3b — NewSession() is check-then-act (TryGetValue -> new -> Add) with no lock, so
//        concurrent calls for the same PlayerId both pass the check and the second Add
//        throws, while concurrent distinct adds corrupt the dictionary and silently
//        lose sessions.
// The concurrent and re-entrancy tests are expected to FAIL against the current code and
// pass once Dispose works over a snapshot and the map is guarded against concurrent
// check-then-act access.
public sealed class PlaybackSessionManagerTests
{
    private const string PlayerId = "player-1";

    private readonly PlaybackSessionManager _manager = CreateManager();
    private readonly AuthUser _user = CreateUser();

    [Fact]
    public void NewSession_SamePlayerIdTwice_ShouldReturnSameSession()
    {
        var first = _manager.NewSession(PlayerId, _user);
        var second = _manager.NewSession(PlayerId, _user);

        first.Session.ShouldBeSameAs(second.Session);
        _manager.PlaybackSessions.ShouldHaveSingleItem();
    }

    [Fact]
    public void Dispose_WithSingleLiveSession_ShouldNotThrowAndRemoveSession()
    {
        _manager.NewSession(PlayerId, _user);

        var exception = Record.Exception(() => _manager.Dispose());

        exception.ShouldBeNull();
        _manager.PlaybackSessions.ShouldBeEmpty();
    }

    [Fact]
    public void Dispose_WithMultipleLiveSessions_ShouldDisposeEverySession()
    {
        _manager.NewSession(PlayerId, _user);
        _manager.NewSession("player-2", _user);
        _manager.NewSession("player-3", _user);

        var exception = Record.Exception(() => _manager.Dispose());

        exception.ShouldBeNull();
        _manager.PlaybackSessions.ShouldBeEmpty();
    }

    [Fact]
    public void Dispose_SessionRegisteredViaUpdateEventDuringDisposal_ShouldNotThrow()
    {
        _manager.NewSession(PlayerId, _user);

        // A subscriber of the manager's UpdateEvent registers a session in response to
        // the event fired by RemoveSession while Dispose() is mid-enumeration. With the
        // unsynchronized dictionary the new Add lands in PlaybackSessions while the
        // foreach is still iterating it and aborts Dispose. (With a snapshot-based
        // dispose the new session simply survives the disposal, which is why this test
        // only asserts the absence of the exception.)
        var created = false;
        _manager.UpdateEvent += () =>
        {
            if (!created)
            {
                created = true;
                _manager.NewSession("player-registered-during-dispose", _user);
            }
        };

        var exception = Record.Exception(() => _manager.Dispose());

        exception.ShouldBeNull();
    }

    [Fact]
    public async Task NewSession_ConcurrentCallsWithSamePlayerId_ShouldCreateOneSessionWithoutExceptions()
    {
        const int workerCount = 64;
        const int iterations = 50;

        var exceptions = new ConcurrentBag<Exception>();
        var sessions = new ConcurrentBag<PlaybackSession>();

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            // Synchronize all workers so they enter the TryGetValue -> Add window together.
            var barrier = new Barrier(workerCount);
            var tasks = Enumerable.Range(0, workerCount)
                .Select(_ => Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    try
                    {
                        sessions.Add(_manager.NewSession(PlayerId, _user).Session);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }))
                .ToArray();

            await Task.WhenAll(tasks);

            if (exceptions.Count > 0)
                break; // The race is already proven; stop hammering.
        }

        exceptions.ShouldBeEmpty();
        _manager.PlaybackSessions.ShouldHaveSingleItem();
        _manager.PlaybackSessions.ShouldContainKey(PlayerId);
        sessions.Distinct().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task NewSession_ConcurrentCallsWithDistinctPlayerIds_ShouldCreateEverySessionWithoutExceptions()
    {
        const int workerCount = 64;

        var exceptions = new ConcurrentBag<Exception>();
        var tasks = Enumerable.Range(0, workerCount)
            .Select(index => Task.Run(() =>
            {
                try
                {
                    _manager.NewSession($"player-{index}", _user);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        exceptions.ShouldBeEmpty();
        _manager.PlaybackSessions.Count.ShouldBe(workerCount);
    }

    private static PlaybackSessionManager CreateManager() => new(
        new TestDbContextFactory(
            new DbContextOptionsBuilder<MediaFeederDataContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options
        ),
        Substitute.For<ILogger<PlaybackSession>>()
    );

    private static AuthUser CreateUser() => new() { Id = 1, Username = "test-player", Email = "test-player" + "@example.com" };
}
