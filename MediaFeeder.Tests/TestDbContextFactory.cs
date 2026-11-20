using MediaFeeder.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder;

// Real IDbContextFactory for unit tests, replacing the broken NSubstitute proxy. Proxying this EF
// Core generic interface — in particular the async CreateDbContextAsync — throws an ArgumentNullException
// deep in Castle DynamicProxy under net10.0, before any test logic runs. Every factory method returns a
// fresh context bound to the supplied (in-memory) options. CreateDbContextAsync additionally records the
// token it was called with so a test can assert on call count / token threading
// (see DownloadVideoConsumerTests, bug low-07c).
public sealed class TestDbContextFactory(DbContextOptions<MediaFeederDataContext> contextOptions)
    : IDbContextFactory<MediaFeederDataContext>
{
    private readonly List<CancellationToken> _createDbContextAsyncCalls = [];

    public IReadOnlyList<CancellationToken> CreateDbContextAsyncCalls => _createDbContextAsyncCalls;

    public MediaFeederDataContext CreateDbContext() => new(contextOptions);

    public Task<MediaFeederDataContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        _createDbContextAsyncCalls.Add(cancellationToken);
        return Task.FromResult(new MediaFeederDataContext(contextOptions));
    }
}
