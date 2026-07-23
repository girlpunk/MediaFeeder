using TickerQ.Utilities.Interfaces;

namespace MediaFeeder.Tasks;

public sealed record DownloadVideoContract<TProvider>(int VideoId)
    where TProvider : IProvider;

public interface IDownloadVideo<TProvider> : ITickerFunction<DownloadVideoContract<TProvider>>
    where TProvider : IProvider;
