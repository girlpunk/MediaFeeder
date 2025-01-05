using MassTransit;
using MassTransit.Scheduling;
using MediaFeeder.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Tasks;

public class SynchroniseAllConsumer(
    ILogger<SynchroniseAllConsumer> logger,
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    IServiceProvider serviceProvider,
    IPublishEndpoint bus)
    : IConsumer<SynchroniseAllContract>
{
    public async Task Consume(ConsumeContext<SynchroniseAllContract> context)
    {
        logger.LogInformation("Starting synchronize all");

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var subscriptions = await db.Subscriptions
            .OrderBy(static s => s.LastSynchronised)
            .Select(static s => new Tuple<int, string>(s.Id, s.Provider).ToValueTuple())
            .ToListAsync(context.CancellationToken);

        var providers = serviceProvider.GetServices<IProvider>()
            .ToLookup(static p => p.ProviderIdentifier);

        foreach (var subscription in subscriptions)
        {
            var providerType = providers[subscription.Item2].Single().GetType();

            var contractType = typeof(SynchroniseSubscriptionContract<>).MakeGenericType(providerType);
            var contract = Activator.CreateInstance(contractType, new object[] { subscription.Item1 });

            await bus.Publish(contract, context.CancellationToken);
        }
    }
}

public class SynchroniseAllSchedule : DefaultRecurringSchedule
{
    public SynchroniseAllSchedule()
    {
        CronExpression = "25 * * * *"; // this means every minute
    }
}
