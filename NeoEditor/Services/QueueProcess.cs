using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NeoEditor.Data.Messages;

namespace NeoEditor.Services;

public abstract class QueueProcess<TIn, TOut>
{
    protected readonly IEventAggregator EventAggregator;
    private bool _running;
    public Action<TIn> OnBegin;
    public Func<Task> OnInit;

    public Action<TOut> OnResult;

    protected QueueProcess(IEventAggregator eventAggregator)
    {
        EventAggregator = eventAggregator;
        OnInit += () =>
        {
            EventAggregator.GetEvent<LoggingEvent>().Publish(
                new LogMessage
                {
                    Level = LogLevel.Warning,
                    Message = $"{GetType().Name} OnInit"
                }
            );
            return Task.CompletedTask;
        };
        OnBegin += inp =>
            EventAggregator.GetEvent<LoggingEvent>().Publish(
                new LogMessage
                {
                    Level = LogLevel.Warning,
                    Message = $"{GetType().Name} OnResult: {inp}"
                }
            );
        OnResult += result =>
            EventAggregator.GetEvent<LoggingEvent>().Publish(
                new LogMessage
                {
                    Level = LogLevel.Warning,
                    Message = $"{GetType().Name} OnResult: {result}"
                }
            );
    }

    public ConcurrentQueue<TIn> QueueIn { get; } = new();

    protected abstract Task<TOut> Processor(TIn item);

    public async Task RunUtilEmpty(CancellationToken cancellationToken)
    {
        if (_running)
        {
            EventAggregator.GetEvent<LoggingEvent>().Publish(
                new LogMessage
                {
                    Level = LogLevel.Warning,
                    Message = "ProcessQueue already running."
                }
            );
            return;
        }

        _running = true;
        try
        {
            await OnInit();
            while (!QueueIn.IsEmpty)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (QueueIn.TryDequeue(out var item))
                {
                    OnBegin(item);
                    if (item != null && await Processor(item) is { } resp)
                        OnResult(resp);
                }
                else
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            EventAggregator.GetEvent<LoggingEvent>().Publish(
                new LogMessage
                {
                    Level = LogLevel.Warning,
                    Message = "ProcessQueue cancelled."
                }
            );
        }
        finally
        {
            _running = false;
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_running)
        {
            EventAggregator.GetEvent<LoggingEvent>().Publish(
                new LogMessage
                {
                    Level = LogLevel.Warning,
                    Message = "ProcessQueue already running."
                }
            );
            return;
        }

        _running = true;
        try
        {
            await OnInit();
            while (_running && !QueueIn.IsEmpty)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (QueueIn.TryDequeue(out var item))
                {
                    OnBegin(item);
                    if (item != null && await Processor(item) is { } resp)
                        OnResult(resp);
                }
                else
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            EventAggregator.GetEvent<LoggingEvent>().Publish(
                new LogMessage
                {
                    Level = LogLevel.Warning,
                    Message = "ProcessQueue cancelled."
                }
            );
        }
        finally
        {
            QueueIn.Clear();
            _running = false;
        }
    }
}