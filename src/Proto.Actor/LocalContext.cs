// -----------------------------------------------------------------------
//  <copyright file="LocalContext.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Mailbox;

namespace Proto
{
    internal enum ContextState
    {
        None,
        Alive,
        Restarting,
        Stopping
    }

    public class Context : IMessageInvoker, IContext, ISupervisor
    {
        private static ILogger Logger { get; } = Log.CreateLogger<Context>();
        public static readonly IReadOnlyCollection<PID> EmptyChildren = new List<PID>();

        private readonly Receive _receiveMiddleware;
        private readonly Sender _senderMiddleware;
        private readonly Func<IActor> _producer;
        private readonly ISupervisorStrategy _supervisorStrategy;
        private FastSet<PID> _children;
        private object _message;
        private ContextState _state;
        private RestartStatistics _restartStatistics;
        private FastSet<PID> _watchers;

        //TODO: I would like to extract these two as optional components in the future
        //for ReceiveTimeout we could have an object with the SetReceiveTimeout
        //and simply let this object subscribe to actor messages so it knows when to reset the timer
        private Timer _receiveTimeoutTimer;
        //for Stashing, there could be an object with the Stash, Unstash and UnstashAll
        //the main concern for this would be how to make the stash survive between actor restarts
        //if it is injected as a dependency, that would work fine
        private Stack<object> _stash;

        public Context(Func<IActor> producer, ISupervisorStrategy supervisorStrategy, Receive receiveMiddleware, Sender senderMiddleware, PID parent)
        {
            _producer = producer;
            _supervisorStrategy = supervisorStrategy;
            _receiveMiddleware = receiveMiddleware;
            _senderMiddleware = senderMiddleware;

            //Parents are implicitly watching the child
            //The parent is not part of the Watchers set
            Parent = parent;

            IncarnateActor();
        }

        public IReadOnlyCollection<PID> Children => _children?.ToList() ?? EmptyChildren;

        public IActor Actor { get; private set; }
        public PID Parent { get; }
        public PID Self { get; internal set; }

        public object Message
        {
            get
            {
                var r = _message as MessageEnvelope;
                return r != null ? r.Message : _message;
            }
        }

        public PID Sender => (_message as MessageEnvelope)?.Sender;

        public MessageHeader Headers
        {
            get
            {
                if (_message is MessageEnvelope messageEnvelope)
                {
                    if (messageEnvelope.Header != null)
                    {
                        return messageEnvelope.Header;
                    }
                }
                return MessageHeader.EmptyHeader;
            }
        }

        public TimeSpan ReceiveTimeout { get; private set; }

        public void Stash()
        {
            if (_stash == null)
            {
                _stash = new Stack<object>();
            }
            _stash.Push(Message);
        }

        public Task RespondAsync(object message)
        {
            return Sender.SendAsync(message);
        }

        public PID Spawn(Props props)
        {
            var id = ProcessRegistry.Instance.NextId();
            return SpawnNamed(props, id);
        }

        public PID SpawnPrefix(Props props, string prefix)
        {
            var name = prefix + ProcessRegistry.Instance.NextId();
            return SpawnNamed(props, name);
        }

        public PID SpawnNamed(Props props, string name)
        {
            var pid = props.Spawn($"{Self.Id}/{name}", Self);
            if (_children == null)
            {
                _children = new FastSet<PID>();
            }
            _children.Add(pid);

           return pid;
        }

        public Task WatchAsync(PID pid)
        {
            return pid.SendSystemMessageAsync(new Watch(Self));
        }

        public Task UnwatchAsync(PID pid)
        {
            return pid.SendSystemMessageAsync(new Unwatch(Self));
        }

        public void SetReceiveTimeout(TimeSpan duration)
        {
            if (duration == ReceiveTimeout)
            {
                return;
            }
            if (duration > TimeSpan.Zero)
            {
                StopReceiveTimeout();
            }
            if (duration < TimeSpan.FromMilliseconds(1))
            {
                duration = TimeSpan.FromMilliseconds(1);
            }
            ReceiveTimeout = duration;
            if (ReceiveTimeout > TimeSpan.Zero)
            {
                if (_receiveTimeoutTimer == null)
                {
                    _receiveTimeoutTimer = new Timer(ReceiveTimeoutCallback, null, ReceiveTimeout, ReceiveTimeout);
                }
                else
                {
                    ResetReceiveTimeout();
                }
            }
        }

        public Task ReceiveAsync(object message)
        {
            return ProcessMessageAsync(message);
        }

        public Task InvokeSystemMessageAsync(object msg)
        {
            try
            {
                switch (msg)
                {
                    case Started s:
                        return InvokeUserMessageAsync(s);
                    case Stop _:
                        return HandleStopAsync();
                    case Terminated t:
                        return HandleTerminatedAsync(t);
                    case Watch w:
                        return HandleWatchAsync(w);
                    case Unwatch uw:
                        HandleUnwatch(uw);
                        return Task.FromResult(0);
                    case Failure f:
                        return HandleFailureAsync(f);
                    case Restart _:
                        return HandleRestartAsync();
                    case SuspendMailbox _:
                        return Task.FromResult(0);
                    case ResumeMailbox _:
                        return Task.FromResult(0);
                    case Continuation cont:
                        _message = cont.Message;
                        return cont.Action();
                    default:
                        Logger.LogWarning("Unknown system message {0}", msg);
                        return Task.FromResult(0);
                }
            }
            catch (Exception x)
            {
                Logger.LogError("Error handling SystemMessage {0}", x);
                throw;
            }
        }

        public Task InvokeUserMessageAsync(object msg)
        {
            var influenceTimeout = true;
            if (ReceiveTimeout > TimeSpan.Zero)
            {
                var notInfluenceTimeout = msg is INotInfluenceReceiveTimeout;
                influenceTimeout = !notInfluenceTimeout;
                if (influenceTimeout)
                {
                    StopReceiveTimeout();
                }
            }

            var res = ProcessMessageAsync(msg);

            if (ReceiveTimeout != TimeSpan.Zero && influenceTimeout)
            {
                //special handle non completed tasks that need to reset ReceiveTimout
                if (!res.IsCompleted)
                {
                    return res.ContinueWith(_ => ResetReceiveTimeout());
                }

                ResetReceiveTimeout();
            }
            return res;
        }

        public Task EscalateFailureAsync(Exception reason, object message)
        {
            return EscalateFailureAsync(Self, reason);
        }

        public async Task EscalateFailureAsync(PID who, Exception reason)
        {
            if (_restartStatistics == null)
            {
                _restartStatistics = new RestartStatistics(0, null);
            }
            var failure = new Failure(who, reason, _restartStatistics);
            if (Parent == null)
            {
                await HandleRootFailureAsync(failure);
            }
            else
            {
                await Self.SendSystemMessageAsync(SuspendMailbox.Instance);
                await Parent.SendSystemMessageAsync(failure);
            }
        }

        public async Task RestartChildrenAsync(params PID[] pids)
        {
            await Task.WhenAll(pids.Select(x => x.SendSystemMessageAsync(Restart.Instance)).ToList());
        }

        public async Task StopChildrenAsync(params PID[] pids)
        {
                await Task.WhenAll(pids.Select(x => x.SendSystemMessageAsync(Stop.Instance)).ToList());
        }

        public async Task ResumeChildrenAsync(params PID[] pids)
        {
            await Task.WhenAll(pids.Select(x => x.SendSystemMessageAsync(ResumeMailbox.Instance)).ToList());
        }

        internal static Task DefaultReceiveAsync(IContext context)
        {
            var c = (Context) context;
            if (c.Message is PoisonPill)
            {
                return c.Self.StopAsync();
            }
            return c.Actor.ReceiveAsync(context);
        }

        internal static Task DefaultSender(ISenderContext context, PID target, MessageEnvelope envelope)
        {
            return target.Ref.SendUserMessageAsync(target, envelope);
        }

        private Task ProcessMessageAsync(object msg)
        {
            _message = msg;
            return _receiveMiddleware != null ? _receiveMiddleware(this) : DefaultReceiveAsync(this);
        }

        public Task SendAsync(PID target, object message)
        {
            return SendUserMessageAsync(target, message);
        }

        public Task RequestAsync(PID target, object message)
        {
            var messageEnvelope = new MessageEnvelope(message, Self, null);
            return SendUserMessageAsync(target, messageEnvelope);
        }

        public Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout)
            => RequestAsync(target, message, new FutureProcess<T>(timeout));

        public Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
            => RequestAsync(target, message, new FutureProcess<T>(cancellationToken));

        public Task<T> RequestAsync<T>(PID target, object message)
            => RequestAsync(target, message, new FutureProcess<T>());

        private async Task<T> RequestAsync<T>(PID target, object message, FutureProcess<T> future)
        {
            var messageEnvelope = new MessageEnvelope(message, future.Pid, null);
            await SendUserMessageAsync(target, messageEnvelope);
            return await future.Task;
        }

        private Task SendUserMessageAsync(PID target, object message)
        {
            if (_senderMiddleware != null)
            {
                if (message is MessageEnvelope messageEnvelope)
                {
                    //Request based middleware
                    return _senderMiddleware(this, target, messageEnvelope);
                }
                else
                {
                    //tell based middleware
                    return _senderMiddleware(this, target, new MessageEnvelope(message, null, null));
                }
            }
            else
            {
                //Default path
                return target.SendAsync(message);
            }
        }

        private void IncarnateActor()
        {
            _state = ContextState.Alive;
            Actor = _producer();
        }

        private async Task HandleRestartAsync()
        {
            _state = ContextState.Restarting;
            await InvokeUserMessageAsync(Restarting.Instance);
            if (_children != null)
            {
                foreach (var child in _children)
                {
                    await child.StopAsync();
                }
            }
            await TryRestartOrTerminateAsync();
        }

        private void HandleUnwatch(Unwatch uw)
        {
            _watchers?.Remove(uw.Watcher);
        }

        private Task HandleWatchAsync(Watch w)
        {
            if (_state == ContextState.Stopping)
            {
                return w.Watcher.SendSystemMessageAsync(new Terminated()
                {
                    Who = Self
                });
            }
            else
            {
                if (_watchers == null)
                {
                    _watchers = new FastSet<PID>();
                }
                _watchers.Add(w.Watcher);
                return Proto.Actor.Done;
            }
        }

        private Task HandleFailureAsync(Failure msg)
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            if (Actor is ISupervisorStrategy supervisor)
            {
                return supervisor.HandleFailureAsync(this, msg.Who, msg.RestartStatistics, msg.Reason);
            }
            return _supervisorStrategy.HandleFailureAsync(this, msg.Who, msg.RestartStatistics, msg.Reason);
        }

        private async Task HandleTerminatedAsync(Terminated msg)
        {
            _children?.Remove(msg.Who);
            await InvokeUserMessageAsync(msg);
            await TryRestartOrTerminateAsync();
        }

        private Task HandleRootFailureAsync(Failure failure)
        {
            return Supervision.DefaultStrategy.HandleFailureAsync(this, failure.Who, failure.RestartStatistics, failure.Reason);
        }

        private async Task HandleStopAsync()
        {
            _state = ContextState.Stopping;
            //this is intentional
            await InvokeUserMessageAsync(Stopping.Instance);
            if (_children != null)
            {
                foreach (var child in _children)
                {
                    await child.StopAsync();
                }
            }
            await TryRestartOrTerminateAsync();
        }

        private async Task TryRestartOrTerminateAsync()
        {
            if (_receiveTimeoutTimer != null)
            {
                StopReceiveTimeout();
                _receiveTimeoutTimer = null;
                ReceiveTimeout = TimeSpan.Zero;
            }

            if (_children?.Count > 0)
            {
                return;
            }

            switch (_state)
            {
                case ContextState.Restarting:
                    await RestartAsync();
                    return;
                case ContextState.Stopping:
                    await StopAsync();
                    break;
            }
        }

        private async Task StopAsync()
        {
            ProcessRegistry.Instance.Remove(Self);
            //This is intentional
            await InvokeUserMessageAsync(Stopped.Instance);

            DisposeActorIfDisposable();

            //Notify watchers
            if (_watchers != null)
            {
                var terminated = new Terminated()
                {
                    Who = Self
                };
                foreach (var watcher in _watchers)
                {
                    await watcher.SendSystemMessageAsync(terminated);
                }
            }
            if (Parent != null)
            {
                var terminated = new Terminated()
                {
                    Who = Self
                };
                await Parent.SendSystemMessageAsync(terminated);
            }
        }

        private async Task RestartAsync()
        {
            DisposeActorIfDisposable();
            IncarnateActor();
            await Self.SendSystemMessageAsync(ResumeMailbox.Instance);

            await InvokeUserMessageAsync(Started.Instance);
            if (_stash != null)
            {
                while (_stash.Any())
                {
                    var msg = _stash.Pop();
                    await InvokeUserMessageAsync(msg);
                }
            }
        }

        private void DisposeActorIfDisposable()
        {
            if (Actor is IDisposable disposableActor)
            {
                disposableActor.Dispose();
            }
        }

        private void ResetReceiveTimeout()
        {
            _receiveTimeoutTimer?.Change(ReceiveTimeout, ReceiveTimeout);
        }

        private void StopReceiveTimeout()
        {
            _receiveTimeoutTimer?.Change(-1, -1);
        }

        private void ReceiveTimeoutCallback(object state)
        {
            Self.RequestAsync(Proto.ReceiveTimeout.Instance, null).GetAwaiter().GetResult();
        }

        public void ReenterAfter<T>(Task<T> target, Func<Task<T>, Task> action)
        {
            var msg = _message;
            var cont = new Continuation(() => action(target), msg);

            target.ContinueWith(t => { Self.SendSystemMessageAsync(cont); });
        }
    }
}
