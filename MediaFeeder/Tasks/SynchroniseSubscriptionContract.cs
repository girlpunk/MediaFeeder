using TickerQ.Utilities.Interfaces;

namespace MediaFeeder.Tasks;

public sealed record SynchroniseSubscriptionContract<TProvider>(int SubscriptionId)
    where TProvider : IProvider;

public interface ISynchroniseSubscription<TProvider> : ITickerFunction<SynchroniseSubscriptionContract<TProvider>> where TProvider : IProvider;
