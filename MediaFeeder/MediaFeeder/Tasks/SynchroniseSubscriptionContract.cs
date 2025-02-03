namespace MediaFeeder.Tasks;

public sealed record SynchroniseSubscriptionContract<TProvider>(int SubscriptionId) where TProvider : IProvider;
