using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MediaFeeder.Data;

public class SlowQueryDetectionHelper(ILogger<SlowQueryDetectionHelper> log) : DbCommandInterceptor
{
    private const int SlowQueryThresholdInMilliSecond = 5000;

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (eventData.Duration.TotalMilliseconds > SlowQueryThresholdInMilliSecond)
            log.LogWarning("Slow Query Detected. {}  TotalMilliSeconds: {}", command.CommandText, eventData.Duration.TotalMilliseconds);

        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        if (eventData.Duration.TotalMilliseconds > SlowQueryThresholdInMilliSecond)
            log.LogWarning("Slow Query Detected. {}  TotalMilliSeconds: {}", command.CommandText, eventData.Duration.TotalMilliseconds);

        return base.ReaderExecuted(command, eventData, result);
    }
}