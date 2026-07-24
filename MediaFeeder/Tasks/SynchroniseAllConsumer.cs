using MediaFeeder.Data;
using Microsoft.EntityFrameworkCore;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces;
using TickerQ.Utilities.Interfaces.Managers;

namespace MediaFeeder.Tasks;

using Helpers;

public class SynchroniseAllConsumer(
    ILogger<SynchroniseAllConsumer> logger,
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    IServiceProvider serviceProvider,
    ITimeTickerManager<TimeTickerEntity> timeTicker
) : ITickerFunction
{
    public async Task ExecuteAsync(TickerFunctionContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting synchronize all");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var subscriptions = await db
            .Subscriptions.Where(static s => !s.DisableSync)
            .OrderBy(static s => s.LastSynchronised)
            .Select(static s => new Tuple<int, string>(s.Id, s.Provider).ToValueTuple())
            .ToListAsync(cancellationToken);

        var providers = serviceProvider
            .GetServices<IProvider>()
            .ToLookup(static p => p.ProviderIdentifier);

        foreach (var subscription in subscriptions)
        {
            var providerType = providers[subscription.Item2].SingleOrDefault();

            if (providerType == null)
            {
                logger.LogError("Could not find a provider for {}", subscription.Item2);
                continue;
            }

            await timeTicker.AddSynchroniseSubscription(subscription.Item1, providerType, logger, cancellationToken);
        }
    }
}
