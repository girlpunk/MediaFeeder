using MediaFeeder.Services;
using MediatR;
using System.Diagnostics;

namespace MediaFeeder.Behaviours;

public class PerformanceBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    private readonly Stopwatch _timer;
    private readonly ILogger<TRequest> _logger;
    private readonly CurrentUserService _currentUserService;
    public PerformanceBehaviour(
        ILogger<TRequest> logger,
        CurrentUserService currentUserService)
    {
        _timer = new Stopwatch();

        _logger = logger;
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _timer.Start();

        var response = await next();

        _timer.Stop();

        var elapsedMilliseconds = _timer.ElapsedMilliseconds;

        if (elapsedMilliseconds > 500)
        {
            var requestName = typeof(TRequest).Name;

            var userName = _currentUserService.DisplayName;
            _logger.LogWarning("{Name} long running request ({ElapsedMilliseconds} milliseconds) with {@Request} {@UserName} ",
                requestName, elapsedMilliseconds, request, userName);
        }
        return response;
    }
}
