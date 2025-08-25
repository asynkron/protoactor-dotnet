// -----------------------------------------------------------------------
//   <copyright file="Endpoint.cs" company="Asynkron AB">
//       Copyright (C) 2015-2025 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Extensions;

namespace Proto.Remote;

public abstract class Endpoint : IEndpoint
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly LogLevel _deserializationErrorLogLevel;
#pragma warning disable CS0618 // Type or member is obsolete
    private readonly ILogger _logger = Log.CreateLogger<Endpoint>();
#pragma warning restore CS0618 // Type or member is obsolete
    private readonly Channel<RemoteDeliver> _remoteDelivers = Channel.CreateUnbounded<RemoteDeliver>();
    private readonly Channel<RemoteDeliver> _remotePriorityDelivers = Channel.CreateUnbounded<RemoteDeliver>();
    private readonly Task _sender;
    private readonly object _synLock = new();
    private readonly Dictionary<string, HashSet<PID>> _watchedActors = new();
    protected readonly string RemoteAddress;
    protected readonly RemoteConfig RemoteConfig;
    protected readonly ActorSystem System;

    internal Endpoint(string remoteAddress, ActorSystem system, RemoteConfig remoteConfig)
    {
        RemoteAddress = remoteAddress;
        System = system;
        RemoteConfig = remoteConfig;
        _sender = Task.Run(RunAsync);
        _deserializationErrorLogLevel = system.Remote().Config.DeserializationErrorLogLevel;
    }

    private CancellationToken CancellationToken => _cancellationTokenSource.Token;

    public Channel<RemoteDeliver[]> Outgoing { get; } = Channel.CreateBounded<RemoteDeliver[]>(3);
    public ConcurrentStack<RemoteDeliver[]> OutgoingStash { get; } = new();

    public virtual async ValueTask DisposeAsync()
    {
        _logger.LogDebug("[{SystemAddress}] Disposing endpoint {Address}", System.Address, RemoteAddress);
        _remoteDelivers.Writer.TryComplete();
        _cancellationTokenSource.Cancel();
        Outgoing.Writer.TryComplete();
        TerminateEndpoint();
        await _sender.ConfigureAwait(false);
        _cancellationTokenSource.Dispose();
        GC.SuppressFinalize(this);
        _logger.LogDebug("[{SystemAddress}] Disposed endpoint {Address}", System.Address, RemoteAddress);
    }

    public bool IsActive { get; private set; } = true;

    public void RemoteUnwatch(PID target, Unwatch unwatch)
    {
        lock (_synLock)
        {
            if (_watchedActors.TryGetValue(target.Id, out var pidSet))
            {
                if (pidSet.Remove(unwatch.Watcher) && pidSet.Count == 0)
                {
                    _watchedActors.Remove(target.Id);
                }
            }

            var w = unwatch.Watcher;
            SendMessage(target, unwatch);
        }
    }

    public void RemoteWatch(PID target, Watch watch)
    {
        lock (_synLock)
        {
            if (_watchedActors.TryGetValue(target.Id, out var pidSet))
            {
                pidSet.Add(watch.Watcher);
            }
            else
            {
                _watchedActors[target.Id] = new HashSet<PID> { watch.Watcher };
            }

            SendMessage(target, watch);
        }
    }

    public void SendMessage(PID target, object msg)
    {
        var (message, sender, header) = Proto.MessageEnvelope.Unwrap(msg);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[{SystemAddress}] Sending message {MessageType} {MessagePayload} to {Target} from {Sender}",
                System.Address,
                message.GetMessageTypeName(), message, target, sender
            );
        }

        if (sender is not null && sender.TryTranslateToProxyPID(System, RemoteAddress, out var clientPID))
        {
            sender = clientPID;
        }

        var env = new RemoteDeliver(header, message, target, sender);
        var didWrite = message switch
        {
            IRemotePriorityMessage => _remotePriorityDelivers.Writer.TryWrite(env),
            _ => _remoteDelivers.Writer.TryWrite(env)
        };

        if (CancellationToken.IsCancellationRequested || !didWrite)
        {
            _logger.LogWarning("[{SystemAddress}] Dropping message {MessageType} {MessagePayload} to {Target} from {Sender}",
                System.Address,
                message.GetMessageTypeName(), message, target, sender
            );

            RejectRemoteDeliver(env);
        }
    }

    public void RemoteTerminate(PID watcher, Terminated terminated)
    {
        lock (_synLock)
        {
            if (_watchedActors.TryGetValue(terminated.Who.Id, out var pidSet))
            {
                if (pidSet.Remove(watcher) && pidSet.Count == 0)
                {
                    _watchedActors.Remove(terminated.Who.Id);
                }
            }

            watcher.SendSystemMessage(System, terminated);
        }
    }

    private void TerminateEndpoint()
    {
        IsActive = false;
        ClearWatchers();
        var droppedMessageCount = 0;

        while (OutgoingStash.TryPop(out var remoteMessages))
        {
            droppedMessageCount += DropMessages(remoteMessages);
        }

        while (Outgoing.Reader.TryRead(out var remoteMessages))
        {
            droppedMessageCount += DropMessages(remoteMessages);
        }

        while (_remoteDelivers.Reader.TryRead(out var rd))
        {
            RejectRemoteDeliver(rd);
            droppedMessageCount++;
        }

        if (droppedMessageCount > 0)
        {
            _logger.LogInformation("[{SystemAddress}] Dropped {Count} messages for {Address}", System.Address,
                droppedMessageCount, RemoteAddress);
        }
    }

    private int DropMessages(RemoteDeliver[] messages)
    {
        foreach (var rd in messages)
        {
            RejectRemoteDeliver(rd);
        }

        return messages.Length;
    }

    private void ClearWatchers()
    {
        lock (_synLock)
        {
            foreach (var (id, pidSet) in _watchedActors)
            {
                var watcherPid = PID.FromAddress(System.Address, id);
                var watcherRef = System.ProcessRegistry.Get(watcherPid);

                if (watcherRef == System.DeadLetter)
                {
                    continue;
                }

                foreach (var terminated in pidSet.Select(
                             pid => new Terminated
                             {
                                 Who = pid,
                                 Why = TerminatedReason.AddressTerminated
                             }
                         ))
                {
                    //send the address Terminated event to the Watcher
                    watcherPid.SendSystemMessage(System, terminated);
                }
            }

            _watchedActors.Clear();
        }
    }

    private void RejectRemoteDeliver(RemoteDeliver env)
    {
        switch (env.Message)
        {
            case PoisonPill or Stop when env.Sender is not null:
                System.Root.Send(env.Sender,
                    new Terminated { Who = env.Target, Why = TerminatedReason.AddressTerminated });

                break;
            case Watch watch:
                watch.Watcher.SendSystemMessage(System,
                    new Terminated { Who = env.Target, Why = TerminatedReason.AddressTerminated });

                break;
            default:
                if (env.Sender is not null)
                {
                    System.Root.Send(env.Sender, new DeadLetterResponse { Target = env.Target });
                }
                else
                {
                    System.EventStream.Publish(new DeadLetterEvent(env.Target, env.Message, env.Sender));
                }

                break;
        }
    }

    private async Task RunAsync()
    {
        var waiter = new MultiTaskReuseWaiter<bool>(
            () => _remoteDelivers.Reader.WaitToReadAsync(CancellationToken),
            () => _remotePriorityDelivers.Reader.WaitToReadAsync(CancellationToken));
        
        while (!CancellationToken.IsCancellationRequested)
        {
            try
            {
                var messages = new List<RemoteDeliver>(RemoteConfig.EndpointWriterOptions.EndpointWriterBatchSize);
                while (true)
                {
                    await waiter.WaitAnyAsync();
                    
                    var i = 0;
                    while (true)
                    {
                        var didWrite = false;
                        RemoteDeliver? remoteDeliver;
                        
                        //we don´t need complete priority, we need "enough" important messages to get over
                        if (i++ % 10 == 0)
                        {
                            if (_remotePriorityDelivers.Reader.TryRead(out remoteDeliver))
                            {
                                messages.Add(remoteDeliver);
                                didWrite = true;
                            }

                            if (messages.Count >= RemoteConfig.EndpointWriterOptions.EndpointWriterBatchSize)
                            {
                                break;
                            }
                        }

                        if (_remoteDelivers.Reader.TryRead(out remoteDeliver))
                        {
                            messages.Add(remoteDeliver);
                            didWrite = true;
                        }

                        if (messages.Count >= RemoteConfig.EndpointWriterOptions.EndpointWriterBatchSize)
                        {
                            break;
                        }

                        if (!didWrite)
                        {
                            break;
                        }
                    }

                    var arr = messages.ToArray();
                    await Outgoing.Writer.WriteAsync(arr, CancellationToken).ConfigureAwait(false);
                    messages.Clear();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ex.CheckFailFast();
                _logger.LogError(ex, "[{SystemAddress}] Error in RunAsync", System.Address);
            }
        }
    }

    
    /// <summary>
    /// Preserves non completed Tasks between <see cref="WaitAnyAsync"/> calls.
    /// This is necessary to prevent memory leak https://github.com/asynkron/protoactor-dotnet/issues/2110 
    /// </summary>
    class MultiTaskReuseWaiter<T>
    {
        private readonly Func<ValueTask<T>>[] _taskFactories;
        private readonly Task<T>?[] _tasks;

        public MultiTaskReuseWaiter(params Func<ValueTask<T>>[] taskFactories)
        {
            _taskFactories = taskFactories;
            _tasks = new Task<T>?[taskFactories.Length];
        }

        public async ValueTask<T> WaitAnyAsync()
        {
            for (var i = 0; i < _taskFactories.Length; i++)
            {
                if (_tasks[i]?.IsCompleted == false)
                    continue;
                
                var vt = _taskFactories[i].Invoke();
                if (vt.IsCompleted)
                    return await vt;
                
                _tasks[i] = vt.AsTask();
            }

            return await await Task.WhenAny(_tasks!);
        }

    }
}