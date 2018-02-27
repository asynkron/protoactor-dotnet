// -----------------------------------------------------------------------
//   <copyright file="LocalContext.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
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
        Stopping,
        Stopped,
    }

    public class LocalContext : IMessageInvoker, IContext, ISupervisor
    {
        public static readonly IReadOnlyCollection<PID> EmptyChildren = new List<PID>();
        private readonly Func<IActor> _producer;

        private readonly Receive _receiveMiddleware;
        private readonly Sender _senderMiddleware;
        private readonly ISupervisorStrategy _supervisorStrategy;
        private FastSet<PID> _children;
        private object _message;

        //TODO: I would like to extract these two as optional components in the future
        //for ReceiveTimeout we could have an object with the SetReceiveTimeout
        //and simply let this object subscribe to actor messages so it knows when to reset the timer
        private Timer _receiveTimeoutTimer;

        private RestartStatistics _restartStatistics;

        //for Stashing, there could be an object with the Stash, Unstash and UnstashAll
        //the main concern for this would be how to make the stash survive between actor restarts
        //if it is injected as a dependency, that would work fine
        private Stack<object> _stash;

        private ContextState _state;
        private FastSet<PID> _watchers;

        public LocalContext(Func<IActor> producer, ISupervisorStrategy supervisorStrategy, Receive receiveMiddleware, Sender senderMiddleware, PID parent)
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

        private static ILogger Logger { get; } = Log.CreateLogger<LocalContext>();

        public IReadOnlyCollection<PID> Children => _children?.ToList() ?? EmptyChildren;

        public IActor Actor { get; private set; }
        public PID Parent { get; }
        public PID Self { get; set; }

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

        public void Respond(object message)
        {
            Sender.Tell(message);
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
            if (props.GuardianStrategy != null)
            {
                throw new ArgumentException("Props used to spawn child cannot have GuardianStrategy.");
            }

            var pid = props.Spawn($"{Self.Id}/{name}", Self);
            if (_children == null)
            {
                _children = new FastSet<PID>();
            }
            _children.Add(pid);

            return pid;
        }

        public void Watch(PID pid)
        {
            pid.SendSystemMessage(new Watch(Self));
        }

        public void Unwatch(PID pid)
        {
            pid.SendSystemMessage(new Unwatch(Self));
        }

        public void SetReceiveTimeout(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "Duration must be greater than zero");
            }

            if (duration == ReceiveTimeout)
            {
                return;
            }

            StopReceiveTimeout();
            ReceiveTimeout = duration;

            if (_receiveTimeoutTimer == null)
            {
                _receiveTimeoutTimer = new Timer(ReceiveTimeoutCallback, null, ReceiveTimeout, ReceiveTimeout);
            }
            else
            {
                ResetReceiveTimeout();
            }
        }

        public void CancelReceiveTimeout()
        {
            if (_receiveTimeoutTimer == null)
            {
                return;
            }
            StopReceiveTimeout();
            _receiveTimeoutTimer = null;
            ReceiveTimeout = TimeSpan.Zero;
        }

        public Task ReceiveAsync(object message)
        {
            return ProcessMessageAsync(message);
        }

        public void Tell(PID target, object message)
        {
            SendUserMessage(target, message);
        }

        public void Forward(PID target)
        {
            if (this._message is SystemMessage)
            {
                //SystemMessage cannot be forwarded
                Logger.LogWarning("SystemMessage cannot be forwarded. {0}", this._message);
                return;
            }
            SendUserMessage(target, _message);
        }

        public void Request(PID target, object message)
        {
            var messageEnvelope = new MessageEnvelope(message, Self, null);
            SendUserMessage(target, messageEnvelope);
        }

        public Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout)
            => RequestAsync(target, message, new FutureProcess<T>(timeout));

        public Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
            => RequestAsync(target, message, new FutureProcess<T>(cancellationToken));

        public Task<T> RequestAsync<T>(PID target, object message)
            => RequestAsync(target, message, new FutureProcess<T>());

        public void ReenterAfter<T>(Task<T> target, Func<Task<T>, Task> action)
        {
            var msg = _message;
            var cont = new Continuation(() => action(target), msg);

            target.ContinueWith(t => { Self.SendSystemMessage(cont); });
        }

        public void ReenterAfter(Task target, Action action)
        {
            var msg = _message;
            var cont = new Continuation(() =>
            {
                action();
                return Task.FromResult(0);
            }, msg);

            target.ContinueWith(t => { Self.SendSystemMessage(cont); });
        }


        public void EscalateFailure(Exception reason, PID who)
        {
            if (_restartStatistics == null)
            {
                _restartStatistics = new RestartStatistics(0, null);
            }
            var failure = new Failure(Self, reason, _restartStatistics);
            Self.SendSystemMessage(SuspendMailbox.Instance);
            if (Parent == null)
            {
                HandleRootFailure(failure);
            }
            else
            {
                Parent.SendSystemMessage(failure);
            }
        }

        public void RestartChildren(Exception reason, params PID[] pids)
        {
            foreach (var pid in pids)
            {
                pid.SendSystemMessage(new Restart(reason));
            }
        }

        public void StopChildren(params PID[] pids)
        {
            foreach (var pid in pids)
            {
                pid.SendSystemMessage(Stop.Instance);
            }
        }

        public void ResumeChildren(params PID[] pids)
        {
            foreach (var pid in pids)
            {
                pid.SendSystemMessage(ResumeMailbox.Instance);
            }
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
                        HandleWatch(w);
                        return Task.FromResult(0);
                    case Unwatch uw:
                        HandleUnwatch(uw);
                        return Task.FromResult(0);
                    case Failure f:
                        HandleFailure(f);
                        return Task.FromResult(0);
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
            if (_state == ContextState.Stopped)
            {
                //already stopped
                Logger.LogError("Actor already stopped, ignore user message {0}", msg);
                return Proto.Actor.Done;
            }

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

        public void EscalateFailure(Exception reason, object message)
        {
            EscalateFailure(reason, Self);
        }

        internal static Task DefaultReceive(IContext context)
        {
            var c = (LocalContext)context;
            if (c.Message is PoisonPill)
            {
                c.Self.Stop();
                return Proto.Actor.Done;
            }
            return c.Actor.ReceiveAsync(context);
        }

        internal static Task DefaultSender(ISenderContext context, PID target, MessageEnvelope envelope)
        {
            target.Ref.SendUserMessage(target, envelope);
            return Task.FromResult(0);
        }

        private Task ProcessMessageAsync(object msg)
        {
            _message = msg;
            return _receiveMiddleware != null ? _receiveMiddleware(this) : DefaultReceive(this);
        }

        private Task<T> RequestAsync<T>(PID target, object message, FutureProcess<T> future)
        {
            var messageEnvelope = new MessageEnvelope(message, future.Pid, null);
            SendUserMessage(target, messageEnvelope);
            return future.Task;
        }

        private void SendUserMessage(PID target, object message)
        {
            if (_senderMiddleware != null)
            {
                if (message is MessageEnvelope messageEnvelope)
                {
                    //Request based middleware
                    _senderMiddleware(this, target, messageEnvelope);
                }
                else
                {
                    //tell based middleware
                    _senderMiddleware(this, target, new MessageEnvelope(message, null, null));
                }
            }
            else
            {
                //Default path
                target.Tell(message);
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
                    child.Stop();
                }
            }
            await TryRestartOrTerminateAsync();
        }

        private void HandleUnwatch(Unwatch uw)
        {
            _watchers?.Remove(uw.Watcher);
        }

        private void HandleWatch(Watch w)
        {
            if (_state >= ContextState.Stopping)
            {
                w.Watcher.SendSystemMessage(new Terminated
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
            }
        }

        private void HandleFailure(Failure msg)
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            if (Actor is ISupervisorStrategy supervisor)
            {
                supervisor.HandleFailure(this, msg.Who, msg.RestartStatistics, msg.Reason);
                return;
            }
            _supervisorStrategy.HandleFailure(this, msg.Who, msg.RestartStatistics, msg.Reason);
        }

        private async Task HandleTerminatedAsync(Terminated msg)
        {
            _children?.Remove(msg.Who);
            await InvokeUserMessageAsync(msg);
            await TryRestartOrTerminateAsync();
        }

        private void HandleRootFailure(Failure failure)
        {
            Supervision.DefaultStrategy.HandleFailure(this, failure.Who, failure.RestartStatistics, failure.Reason);
        }

        private async Task HandleStopAsync()
        {
            if (_state >= ContextState.Stopping)
            {
                //already stopping or stopped
                return;
            }

            _state = ContextState.Stopping;
            //this is intentional
            await InvokeUserMessageAsync(Stopping.Instance);
            if (_children != null)
            {
                foreach (var child in _children)
                {
                    child.Stop();
                }
            }
            await TryRestartOrTerminateAsync();
        }

        private async Task TryRestartOrTerminateAsync()
        {
            CancelReceiveTimeout();

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
                var terminated = new Terminated
                {
                    Who = Self
                };
                foreach (var watcher in _watchers)
                {
                    watcher.SendSystemMessage(terminated);
                }
            }
            if (Parent != null)
            {
                var terminated = new Terminated
                {
                    Who = Self
                };
                Parent.SendSystemMessage(terminated);
            }

            _state = ContextState.Stopped;
        }

        private async Task RestartAsync()
        {
            DisposeActorIfDisposable();
            IncarnateActor();
            Self.SendSystemMessage(ResumeMailbox.Instance);

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
            if (_receiveTimeoutTimer == null)
            {
                return;
            }
            CancelReceiveTimeout();
            Self.Request(Proto.ReceiveTimeout.Instance, null);
        }
    }
}
