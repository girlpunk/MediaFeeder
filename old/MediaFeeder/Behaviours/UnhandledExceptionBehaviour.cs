using MediaFeeder.Services;
using MediatR;

namespace MediaFeeder.Behaviours;

public class UnhandledExceptionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    private readonly ILogger<TRequest> _logger;
    private readonly CurrentUserService _currentUserService;

    public UnhandledExceptionBehaviour(ILogger<TRequest> logger, CurrentUserService currentUserService)
    {
        _logger = logger;
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            var requestName = typeof(TRequest).Name;
            var userName = _currentUserService.DisplayName;
            _logger.LogError(ex, "{Name}: {Exception} with {@Request} by {@UserName}", requestName, ex.Message, request, userName);
            throw;
        }
    }
}
