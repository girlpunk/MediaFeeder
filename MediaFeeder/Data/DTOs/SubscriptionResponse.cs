namespace MediaFeeder.Data.DTOs;

[Serializable]
public sealed record SubscriptionResponse(string Name, int Id, int Unwatched);
