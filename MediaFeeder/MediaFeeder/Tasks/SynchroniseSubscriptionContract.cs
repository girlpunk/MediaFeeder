namespace MediaFeeder.Tasks;

public record SynchroniseSubscriptionContract<TProvider>(int SubscriptionId) where TProvider : IProvider;
