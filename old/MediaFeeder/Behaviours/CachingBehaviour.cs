using LazyCache;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace MediaFeeder.Behaviours;

public class CachingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ICacheable
{
    private readonly IAppCache _cache;
    private readonly ILogger<CachingBehaviour<TRequest, TResponse>> _logger;

    public CachingBehaviour(
        IAppCache cache,
        ILogger<CachingBehaviour<TRequest, TResponse>> logger
    )
    {
        _cache = cache;
        _logger = logger;
    }
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{Name} is caching with {@Request}.", nameof(request), request);
        var response = await _cache.GetOrAddAsync(
            request.CacheKey,
            async () =>
                await next(),
            request.Options);

        return response;
    }
}
public interface ICacheable
{
    string CacheKey { get => String.Empty; }
    MemoryCacheEntryOptions? Options { get; }
}

