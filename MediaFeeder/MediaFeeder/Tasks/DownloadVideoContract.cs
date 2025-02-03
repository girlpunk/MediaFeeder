namespace MediaFeeder.Tasks;

public sealed record DownloadVideoContract<TProvider>(int VideoId) where TProvider : IProvider;
