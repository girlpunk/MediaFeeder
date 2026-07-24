namespace MediaFeeder.Helpers;

using System.Reflection;
using Tasks;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models;

internal static class TimerQHelper
{
    public static async Task<TickerResult<TimeTickerEntity>?> AddSynchroniseSubscription(
        this ITimeTickerManager<TimeTickerEntity> timeTicker,
        int subscriptionId,
        IProvider provider,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (provider.SubscriptionSynchroniseType == null)
        {
            logger.LogError($"No synchroniser available for provider {provider.Name}");
            return null;
        }

        var contract = new SynchroniseSubscriptionContract(subscriptionId);

        var queue = typeof(ITimeTickerManager<TimeTickerEntity>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(static m => m.Name == "AddAsync" && m.ContainsGenericParameters && m.GetParameters().Length == 3);
        queue = queue.MakeGenericMethod(provider.SubscriptionSynchroniseType, typeof(SynchroniseSubscriptionContract));

        return await (Task<TickerResult<TimeTickerEntity>>) queue.Invoke(timeTicker, [DateTime.Now, contract, cancellationToken]);
    }

    public static async Task<TickerResult<TimeTickerEntity>?> AddDownloadVideo(
        this ITimeTickerManager<TimeTickerEntity> timeTicker,
        int videoId,
        IProvider provider,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (provider.VideoDownloadType == null)
        {
            logger.LogError($"No downloader available for provider {provider.Name}");
            return null;
        }

        var synchroniseFunctionType = provider.VideoDownloadType;
        var contract = new DownloadVideoContract(videoId);

        var queue = typeof(ITimeTickerManager<TimeTickerEntity>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(static m => m.Name == "AddAsync" && m.ContainsGenericParameters && m.GetParameters().Length == 3);
        queue = queue.MakeGenericMethod(synchroniseFunctionType, typeof(DownloadVideoContract));

        return await (Task<TickerResult<TimeTickerEntity>>) queue.Invoke(timeTicker, [DateTime.Now, contract, cancellationToken]);
    }
}
