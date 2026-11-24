using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using AntDesign;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;
using MediaFeeder.PlaybackManager;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using NSubstitute;
using Shouldly;
using DbVideo = MediaFeeder.Data.db.Video;

namespace MediaFeeder.Components.Pages;

// Verifies bug func-04 (bugs/functional/func-04-video-page-event-leak-cross-thread-save.md).
//
// The /video/{id}/{next} route component is reused across playlist navigations, so OnParametersSetAsync
// re-runs for every video and re-subscribes to the session's UpdateEvent unconditionally - after N
// videos every session update fires UpdateTimestamp N times. UpdateTimestamp then runs on whatever
// thread set the session property (notably YouTubeVideoFrame.ProgressUpdate inside a Task.Run) and
// calls Context.SaveChangesAsync() fire-and-forget on the circuit-scoped MediaFeederDataContext - the
// same instance the UI thread uses for queries and saves. That races into "A second operation was
// started on this context instance before a previous operation completed", which is silently lost
// because the task is discarded, and playback positions stop persisting.
//
// The tests drive the real Video page. Its StateHasChanged/InvokeAsync require a render handle, so
// the component is mounted as the root of a minimal real Renderer (render output discarded, render
// faults captured) on a dispatcher thread standing in for the circuit UI thread (bUnit is not part
// of the test stack). The dispatcher thread installs a SynchronizationContext so all awaits made
// inside OnParametersSetAsync resume on it, as they do in a real circuit. Like the func-03 tests,
// these are expected to FAIL against the current code:
//
//   1. OnParametersSetAsync run twice must leave exactly one UpdateEvent subscription on the session
//      reference (currently: one per navigation, forever).
//   2. A session position update fired from a thread-pool thread (the YouTube progress-update path)
//      must not run a circuit-context save on that foreign thread (currently: it does).
//   3. An off-thread position save racing a UI-thread context operation must not fault the context
//      with the "second operation" exception and must persist the position (currently: the racing
//      save faults with exactly that).
//   4. A malformed Next parameter (e.g. /video/1/abc) must not throw FormatException and take the
//      circuit down (currently: int.Parse throws).
//
// Note: the synchronous EF query for the up-next duration (Context.Videos.Where(...).Sum(...)) is a
// listed issue but is not unit-observable here - the InMemory provider cannot distinguish a
// client-side Sum from an awaited SumAsync - so it is deliberately not covered.
public sealed class VideoPageEventLeakTests
{
    [Fact]
    public async Task OnParametersSetAsync_ComponentReusedAcrossNavigation_ShouldSubscribeUpdateEventExactlyOnce()
    {
        await using var harness = await CreateHarnessAsync();

        await harness.NavigateAsync(id: 1, next: "2");
        await harness.NavigateAsync(id: 2, next: null);

        var updateEventHandlers = CountEventHandlers(GetPageSessionReference(harness.Page), "UpdateEvent");

        updateEventHandlers.ShouldBe(1,
            "reusing the Video page for the next video must not stack another UpdateEvent subscription on the session reference");
    }

    [Fact]
    public async Task UpdateTimestamp_SessionUpdatedFromForeignThread_ShouldNotRunCircuitContextSaveOnThatThread()
    {
        await using var harness = await CreateHarnessAsync();
        await harness.NavigateAsync(id: 1, next: null);
        ForceSaveWindowOpen(harness.Page); // open the 60-second save window without waiting a minute

        // What YouTubeVideoFrame.ProgressUpdate does inside a Task.Run (reproduction step 1).
        var foreignThread = 0;
        var foreignUpdate = Task.Run(() =>
        {
            foreignThread = Environment.CurrentManagedThreadId;
            harness.Session.CurrentPosition = TimeSpan.FromSeconds(120);
        });
        await foreignUpdate;

        harness.Probe.SaveThreads.ShouldNotContain(
            foreignThread,
            "the circuit-scoped context must never be saved from the thread that fired the session update; "
                + "marshal the save to the circuit thread or use an IDbContextFactory context");
    }

    [Fact]
    public async Task UpdateTimestamp_CircuitDisposedWhileOffThreadSaveInFlight_ShouldPersistPosition()
    {
        await using var harness = await CreateHarnessAsync(saveDelayMs: 250);
        await harness.NavigateAsync(id: 1, next: null);
        ForceSaveWindowOpen(harness.Page);

        // A foreign-thread position update starts the page's fire-and-forget context save;
        // the 250 ms probe window keeps that save in flight deterministically.
        harness.Probe.Reset(); // the seed save from CreateHarnessAsync would otherwise count as a save in flight
        var offThreadUpdate = Task.Run(() => harness.Session.CurrentPosition = TimeSpan.FromSeconds(120));
        await WaitUntilAsync(() => harness.Probe.SaveThreads.Count > 0); // the off-thread save has started its 250 ms in-flight window

        // The circuit goes down mid-save: a save bypassing the circuit (fire-and-forget on a
        // foreign thread) survives only until the context is released.
        await harness.ShutdownCircuitAsync();
        await offThreadUpdate; // the position update completes; the orphaned save faults on the disposed context (unobserved)

        DbVideo stored;
        await using (var fresh = harness.CreateFreshContext())
        {
            stored = await fresh.Videos.SingleAsync(static v => v.Id == 1);
        }

        stored.PlaybackPosition.ShouldBe(120, "the position must survive a circuit that goes down mid-save; "
            + "with the off-thread fire-and-forget save it faults with ObjectDisposedException on an unobserved "
            + "task and the position is silently lost");
    }

    [Fact]
    public async Task OnParametersSetAsync_MalformedNextParameter_ShouldNotThrow()
    {
        await using var harness = await CreateHarnessAsync();

        var exception = await Record.ExceptionAsync(() => harness.DirectSetParametersAsync(id: 1, next: "2,abc"));

        exception.ShouldBeNull("a typo'd or crafted next parameter (e.g. /video/1/abc) must not take the circuit down");
    }

    private static async Task<PageHarness> CreateHarnessAsync(int saveDelayMs = 0)
    {
        var fixture = new Fixture().Customize(new AutoNSubstituteCustomization());

        var probe = new SaveProbe();
        var databaseName = "mediafeeder-func04-" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<MediaFeederDataContext>()
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(probe)
            .Options;

        var context = new MediaFeederDataContext(options);
        var user = new AuthUser { Id = 1, Username = "func04-user", Email = "__VG_EMAIL_9e51f1a0b347__" };
        var folder = new Folder { Id = 1, Name = "Root Folder", UserId = 1 };
        var subscription = new Subscription
        {
            Id = 1,
            Name = "Test Channel",
            PlaylistId = "playlist-1",
            Description = "test channel",
            ChannelId = "channel-1",
            ChannelName = "Test Channel",
            Provider = "YouTube",
            UserId = 1,
            ParentFolderId = 1,
        };
        var firstVideo = new DbVideo
        {
            Id = 1,
            SubscriptionId = 1,
            VideoId = "video-1",
            Name = "Video One",
            Description = "first video",
            UploaderName = "Uploader",
            Duration = 600,
            PlaybackPosition = 0,
            PublishDate = DateTimeOffset.UnixEpoch,
        };
        var secondVideo = new DbVideo
        {
            Id = 2,
            SubscriptionId = 1,
            VideoId = "video-2",
            Name = "Video Two",
            Description = "second video",
            UploaderName = "Uploader",
            Duration = 600,
            PlaybackPosition = 0,
            PublishDate = DateTimeOffset.UnixEpoch,
        };
        context.AddRange(user, folder, subscription, firstVideo, secondVideo);
        await context.SaveChangesAsync();
        probe.SaveDelayMs = saveDelayMs; // the seed save above must not be delayed

        var manager = new PlaybackSessionManager(
            new TestDbContextFactory(options),
            Substitute.For<ILogger<PlaybackSession>>()
        );

        var dispatcher = new UiCircuitDispatcher();

        var authenticationStateProvider = fixture.Create<AuthenticationStateProvider>();
        authenticationStateProvider
            .GetAuthenticationStateAsync()
            .Returns(
                Task.FromResult(
                    new AuthenticationState(
                        new ClaimsPrincipal(
                            [
                                new ClaimsIdentity(
                                    [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())]
                                )
                            ]
                        )
                    )
                )
            );

        var userManager = Substitute.For<UserManager<AuthUser>>(
            Substitute.For<IUserStore<AuthUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<AuthUser>>(),
            Array.Empty<IUserValidator<AuthUser>>(),
            Array.Empty<IPasswordValidator<AuthUser>>(),
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<AuthUser>>>()
        );
        userManager.GetUserAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult(user));

        var provider = fixture.Create<IProvider>();
        provider.ProviderIdentifier.Returns("YouTube");
        provider.Provider.Returns(Provider.YouTube);
        provider.Name.Returns("YouTube");
        provider.Icon.Returns("yt");
        provider.VideoFrameView.Returns(typeof(TestVideoFrame));

        var services = new ServiceCollection()
            .AddSingleton<IProvider>(provider)
            .AddSingleton<Dispatcher>(dispatcher)
            .AddSingleton(_ => Substitute.For<IJSRuntime>())
            .BuildServiceProvider();

        var page = new Video
        {
            NavigationManager = fixture.Create<NavigationManager>(),
            MessageService = fixture.Create<MessageService>(),
            AuthenticationStateProvider = authenticationStateProvider,
            UserManager = userManager,
            SessionManager = manager,
            SessionIdProvider = new SessionIdProvider(),
        };
        // Razor-generated @inject properties: Razor emits them as private, so set them via reflection.
        SetPrivateProperty(page, "Context", context);
        SetPrivateProperty(page, "ServiceProvider", services);

        var renderer = new TestRenderer(dispatcher, services);
        await renderer.AttachRootAsync(page);

        return new PageHarness(page, manager, context, probe, dispatcher, renderer, services, databaseName);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = Environment.TickCount + timeoutMs;
        while (!condition() && Environment.TickCount < deadline)
            await Task.Delay(10);

        Assert.True(condition(), $"condition was not met within {timeoutMs} ms");
    }

    private static void SetPrivateProperty(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{target.GetType().Name}.{propertyName} was renamed or removed.");

        property.SetValue(target, value);
    }

    private static PlaybackSession.PlaybackSessionReference GetPageSessionReference(Video page)
    {
        var property = typeof(Video).GetProperty("PlaybackSession", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Video.PlaybackSession (private) was renamed or removed.");

        return (PlaybackSession.PlaybackSessionReference) property.GetValue(page)!;
    }

    private static int CountEventHandlers(PlaybackSession.PlaybackSessionReference reference, string eventName)
    {
        var referenceType = typeof(PlaybackSession.PlaybackSessionReference);
        // Compiler versions differ on auto-event backing field names: plain name or <name>k__BackingField.
        var backingField = referenceType.GetField(eventName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            ?? referenceType.GetField($"<{eventName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Event {eventName} no longer has its compiler-generated backing field.");

        return ((Delegate?) backingField.GetValue(reference))?.GetInvocationList().Length ?? 0;
    }

    // The page only saves a position once lastSavePositionTime has run for over 60 seconds.
    // Rewind that stopwatch without waiting, so the save path is reachable in a unit test.
    private static void ForceSaveWindowOpen(Video page)
    {
        var stopwatchField = typeof(Video).GetField("lastSavePositionTime", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Video.lastSavePositionTime (private) was renamed or removed.");
        // Value types are boxed on read; the mutated box must be written back to the page's field.
        var stopwatch = (Stopwatch) stopwatchField.GetValue(page)!;
        // _elapsed in modern .NET (10+), _elapsedTicks in older runtimes.
        var elapsedTicks = typeof(Stopwatch).GetField("_elapsed", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typeof(Stopwatch).GetField("_elapsedTicks", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(".NET's Stopwatch internal layout changed; update ForceSaveWindowOpen().");

        elapsedTicks.SetValue(stopwatch, Stopwatch.Frequency * 61); // 61 s of elapsed time > the 60 s save window
        stopwatchField.SetValue(page, stopwatch);
    }

    private sealed class PageHarness : IAsyncDisposable
    {
        private readonly TestRenderer _renderer;

        public PageHarness(
            Video page,
            PlaybackSessionManager manager,
            MediaFeederDataContext context,
            SaveProbe probe,
            UiCircuitDispatcher dispatcher,
            TestRenderer renderer,
            ServiceProvider services,
            string databaseName
        )
        {
            Page = page;
            Manager = manager;
            Context = context;
            Probe = probe;
            Dispatcher = dispatcher;
            _renderer = renderer;
            Services = services;
            DatabaseName = databaseName;
        }

        public Video Page { get; }

        public PlaybackSessionManager Manager { get; }

        public MediaFeederDataContext Context { get; }

        public SaveProbe Probe { get; }

        public UiCircuitDispatcher Dispatcher { get; }

        public ServiceProvider Services { get; }

        public string DatabaseName { get; }

        public PlaybackSession Session => Manager.PlaybackSessions.Single().Value;

        // A route navigation to the page: the renderer drives the root component. The first call
        // mounts the component; later calls re-drive the same instance with new parameters, which
        // is exactly what the router does when /video/{id} changes to /video/{other}.
        public Task NavigateAsync(int id, string? next) =>
            _renderer.DriveRootAsync(Page, BuildParameters(id, next));

        // Parameters delivered to the component directly (on the circuit thread), proving the
        // OnParametersSetAsync contract independently of the render pipeline.
        public Task DirectSetParametersAsync(int id, string? next) =>
            Dispatcher.InvokeAsync(() => Page.SetParametersAsync(BuildParameters(id, next)));

        private static ParameterView BuildParameters(int id, string? next) =>
            ParameterView.FromDictionary(new Dictionary<string, object?> { ["Id"] = id, ["Next"] = next });

        public MediaFeederDataContext CreateFreshContext() => new(
            new DbContextOptionsBuilder<MediaFeederDataContext>().UseInMemoryDatabase(DatabaseName).Options);

        private bool _circuitStopped;

        private bool _contextDisposed;

        // Simulates the circuit going down: components dispose, the circuit drains, and the
        // circuit-scoped context is released (the DI scope teardown order).
        public ValueTask ShutdownCircuitAsync()
        {
            StopCircuit();
            return DisposeContextAsync();
        }

        private void StopCircuit()
        {
            if (_circuitStopped)
            {
                return;
            }

            _circuitStopped = true;
            // Renderer.Dispose() marshals the component-state disposal (which disposes the page)
            // to the dispatcher; the dispatcher must still be up, so the order matters.
            _renderer.Dispose();
            Dispatcher.Shutdown();
        }

        private ValueTask DisposeContextAsync()
        {
            if (_contextDisposed)
            {
                return ValueTask.CompletedTask;
            }

            _contextDisposed = true;
            return Context.DisposeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            StopCircuit();
            await DisposeContextAsync();
            Services.Dispose();
        }
    }

    // The stand-in for the circuit synchronization context. Work items run on one dedicated
    // thread (the test's "UI thread") in queue order, exactly like a real circuit serializes its
    // work. StateHasChanged/InvokeAsync on the attached component dispatch here, and a
    // SynchronizationContext is installed on that thread so continuation code stays on it.
    private sealed class UiCircuitDispatcher : Dispatcher
    {
        private readonly BlockingCollection<Func<Task>> _workItems = new();
        private readonly Thread _uiThread;

        public int UiThreadId { get; private set; }

        public UiCircuitDispatcher()
        {
            _uiThread = new Thread(Pump)
            {
                IsBackground = true,
                Name = "func04-circuit-ui",
            };
            _uiThread.Start();

            var ready = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            InvokeAsync(() => ready.SetResult(Environment.CurrentManagedThreadId));
            if (!ready.Task.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The circuit UI thread did not start in time.");

            UiThreadId = ready.Task.Result;
        }

        public override bool CheckAccess() => Thread.CurrentThread.ManagedThreadId == UiThreadId;

        public override Task InvokeAsync(Action workItem) => EnqueueVoid(() =>
        {
            workItem();
            return Task.CompletedTask;
        });

        public override Task InvokeAsync(Func<Task> workItem) => EnqueueVoid(workItem);

        public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem) =>
            Enqueue(() => Task.FromResult(workItem()));

        public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem) => Enqueue(workItem);

        private Task EnqueueVoid(Func<Task> workItem)
        {
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _workItems.Add(
                () =>
                {
                    try
                    {
                        var result = workItem();
                        if (result.IsCompleted)
                        {
                            FinishVoid(completed, result);
                            return Task.CompletedTask;
                        }

                        return result.ContinueWith(static (t, state) => FinishVoid((TaskCompletionSource) state!, t), completed, TaskScheduler.Default);
                    }
                    catch (Exception e)
                    {
                        completed.TrySetException(e);
                        return Task.CompletedTask;
                    }
                });
            return completed.Task;
        }

        private Task<TResult> Enqueue<TResult>(Func<Task<TResult>> workItem)
        {
            var completed = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _workItems.Add(
                () =>
                {
                    try
                    {
                        var result = workItem();
                        if (result.IsCompleted)
                        {
                            Finish(completed, result);
                            return Task.CompletedTask;
                        }

                        return result.ContinueWith(static (t, state) => Finish((TaskCompletionSource<TResult>) state!, (Task<TResult>) t), completed, TaskScheduler.Default);
                    }
                    catch (Exception e)
                    {
                        completed.TrySetException(e);
                        return Task.CompletedTask;
                    }
                });
            return completed.Task;
        }

        private static void FinishVoid(TaskCompletionSource completed, Task result)
        {
            if (result.IsFaulted)
                completed.TrySetException(result.Exception!.InnerException!);
            else if (result.IsCanceled)
                completed.TrySetCanceled();
            else
                completed.TrySetResult();
        }

        private static void Finish<TResult>(TaskCompletionSource<TResult> completed, Task<TResult> result)
        {
            if (result.IsFaulted)
                completed.TrySetException(result.Exception!.InnerException!);
            else if (result.IsCanceled)
                completed.TrySetCanceled();
            else
                completed.TrySetResult(result.Result);
        }

        private void Pump()
        {
            // Awaits made while work runs on this thread must resume on this thread, exactly like
            // the circuit's synchronization context; the renderer's Dispatcher.AssertAccess()
            // requires it (AddToRenderQueue/AddPendingTask run on continuation threads otherwise).
            SynchronizationContext.SetSynchronizationContext(new CircuitSynchronizationContext(this));
            foreach (var workItem in _workItems.GetConsumingEnumerable())
                workItem();
        }

        public void Shutdown()
        {
            _workItems.CompleteAdding();
            _uiThread.Join(TimeSpan.FromSeconds(2));
        }

        private sealed class CircuitSynchronizationContext(Dispatcher dispatcher) : SynchronizationContext
        {
            public override void Post(SendOrPostCallback sendOrPostCallback, object? state) =>
                _ = dispatcher.InvokeAsync(() => sendOrPostCallback(state));

            public override void Send(SendOrPostCallback sendOrPostCallback, object? state)
            {
                if (dispatcher.CheckAccess())
                    sendOrPostCallback(state);
                else
                    throw new InvalidOperationException("The test circuit does not support synchronous dispatch from off-thread code.");
            }
        }
    }

    // A minimal real Renderer. It exists so the page gets a render handle and its parameter
    // lifecycles run on the circuit thread; render output is discarded and render faults are
    // captured instead of taking the host down.
    private sealed class TestRenderer(Dispatcher dispatcher, IServiceProvider services)
        : Renderer(services, CreateSilentLoggerFactory())
    {
        private static ILoggerFactory CreateSilentLoggerFactory()
        {
            var factory = Substitute.For<ILoggerFactory>();
            factory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());

            return factory;
        }

        private IComponent? _rootComponent;

        private int? _rootComponentId;

        public ConcurrentBag<Exception> UnhandledExceptions { get; } = new();

        public override Dispatcher Dispatcher => dispatcher;

        protected override void HandleException(Exception exception) => UnhandledExceptions.Add(exception);

        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;

        public Task AttachRootAsync(IComponent component) =>
            Dispatcher.InvokeAsync(() =>
            {
                _rootComponent = component;
                _rootComponentId = AssignRootComponentId(component);
            });

        public async Task DriveRootAsync(IComponent component, ParameterView parameters)
        {
            // The harness attaches the root component exactly once before navigating.
            if (!ReferenceEquals(_rootComponent, component) || _rootComponentId is null)
                throw new InvalidOperationException("DriveRootAsync requires the component to be attached via AttachRootAsync first.");

            await Dispatcher.InvokeAsync(async () => await RenderRootComponentAsync(_rootComponentId.Value, parameters));
        }
    }

    // Records which managed thread ran SaveChanges on the circuit context, and optionally
    // stretches the operation's in-flight window so concurrent operations overlap deterministically.
    private sealed class SaveProbe : SaveChangesInterceptor
    {
        public int SaveDelayMs { get; set; }

        public ConcurrentQueue<int> SaveThreads { get; } = new();

        public void Reset() => SaveThreads.Clear();

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            SaveThreads.Enqueue(Environment.CurrentManagedThreadId);
            Delay();
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            SaveThreads.Enqueue(Environment.CurrentManagedThreadId);
            Delay();
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void Delay()
        {
            if (SaveDelayMs > 0)
                Task.Delay(SaveDelayMs).GetAwaiter().GetResult();
        }
    }

    // Stand-in for the provider's video frame component (rendered by the page's DynamicComponent).
    private sealed class TestVideoFrame : ComponentBase
    {
        [Parameter]
        public object? Video { get; set; }

        [Parameter]
        public object? Page { get; set; }

        [Parameter]
        public object? PlaybackSession { get; set; }
    }
}
