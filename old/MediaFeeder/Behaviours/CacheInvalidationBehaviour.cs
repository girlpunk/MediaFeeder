using LazyCache;
using MediatR;

namespace MediaFeeder.Behaviours;

public class CacheInvalidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ICacheInvalidator
{
    private readonly IAppCache _cache;
    private readonly ILogger<CacheInvalidationBehaviour<TRequest, TResponse>> _logger;

    public CacheInvalidationBehaviour(
        IAppCache cache,
        ILogger<CacheInvalidationBehaviour<TRequest, TResponse>> logger
    )
    {
        _cache = cache;
        _logger = logger;
    }
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{Name} cache expire with {@Request}.", nameof(request), request);
        var response = await next();
        if (!string.IsNullOrEmpty(request.CacheKey))
        {
            _cache.Remove(request.CacheKey);
        }
        request.SharedExpiryTokenSource?.Cancel();
        return response;
    }
}

public interface ICacheInvalidator
{
    string CacheKey { get => String.Empty; }
    CancellationTokenSource? SharedExpiryTokenSource { get => null; }
}
