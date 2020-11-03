// -----------------------------------------------------------------------
//   <copyright file="ActorContext.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Future;
using Proto.Mailbox;

namespace Proto.Context
{
    public class ActorContext : IMessageInvoker, IContext, ISupervisor
    {
        private static readonly ImmutableHashSet<PID> EmptyChildren = ImmutableHashSet<PID>.Empty;
        private readonly Props _props;

        private ActorContextExtras? _extras;
        private object? _messageOrEnvelope;
        private ContextState _state;

        public static ActorContext Setup(ActorSystem system, Props props, PID? parent, PID self) =>
            new ActorContext(system, props, parent, self);

        public ActorContext(ActorSystem system, Props props, PID? parent, PID self)
        {
            System = system;
            _props = props;

            //Parents are implicitly watching the child
            //The parent is not part of the Watchers set
            Parent = parent;
            Self = self;
            Actor = IncarnateActor();
        }

        private static ILogger Logger { get; } = Log.CreateLogger<ActorContext>();

        public ActorSystem System { get; }
        public CancellationTokenSource? CancellationTokenSource => _extras?.CancellationTokenSource;
        public CancellationToken CancellationToken => EnsureExtras().CancellationTokenSource.Token;
        IReadOnlyCollection<PID> IContext.Children => Children;

        public IActor? Actor { get; private set; }
        public PID? Parent { get; }
        public PID Self { get; }

        public object? Message => MessageEnvelope.UnwrapMessage(_messageOrEnvelope);

        public PID? Sender => MessageEnvelope.UnwrapSender(_messageOrEnvelope);

        public MessageHeader Headers => MessageEnvelope.UnwrapHeader(_messageOrEnvelope);

        public TimeSpan ReceiveTimeout { get; private set; }

        public void Stash()
        {
            if (_messageOrEnvelope != null)
            {
                EnsureExtras().Stash.Push(_messageOrEnvelope);
            }
        }

        public void Respond(object message)
        {
            if (Sender != null)
            {
                Logger.LogDebug("{Self} Responding to {Sender} with message {Message}", Self, Sender, message);
                Send(Sender, message);
            }
            else
            {
                Logger.LogWarning("{Self} Tried to respond but sender is null, with message {Message}", Self, message);
            }
        }

        public PID Spawn(Props props)
        {
            var id = System.ProcessRegistry.NextId();
            return SpawnNamed(props, id);
        }

        public PID SpawnPrefix(Props props, string prefix)
        {
            var name = prefix + System.ProcessRegistry.NextId();
            return SpawnNamed(props, name);
        }

        public PID SpawnNamed(Props props, string name)
        {
            if (props.GuardianStrategy != null)
            {
                throw new ArgumentException("Props used to spawn child cannot have GuardianStrategy.");
            }

            var pid = props.Spawn(System, $"{Self.Id}/{name}", Self);
            EnsureExtras().AddChild(pid);

            return pid;
        }

        public void Watch(PID pid) => pid.SendSystemMessage(System, new Watch(Self));

        public void Unwatch(PID pid) => pid.SendSystemMessage(System, new Unwatch(Self));

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

            ReceiveTimeout = duration;

            EnsureExtras();
            _extras!.StopReceiveTimeoutTimer();

            if (_extras.ReceiveTimeoutTimer == null)
            {
                _extras.InitReceiveTimeoutTimer(
                    new Timer(
                        ReceiveTimeoutCallback, null, ReceiveTimeout,
                        ReceiveTimeout
                    )
                );
            }
            else
            {
                _extras.ResetReceiveTimeoutTimer(ReceiveTimeout);
            }
        }

        public void CancelReceiveTimeout()
        {
            if (_extras?.ReceiveTimeoutTimer == null)
            {
                return;
            }

            _extras.StopReceiveTimeoutTimer();
            _extras.KillReceiveTimeoutTimer();

            ReceiveTimeout = TimeSpan.Zero;
        }

        public void Send(PID target, object message) => SendUserMessage(target, message);

        public void Forward(PID target)
        {
            switch (_messageOrEnvelope)
            {
                case null:
                    Logger.LogWarning("Message is null.");
                    return;
                case SystemMessage _:
                    Logger.LogWarning("SystemMessage cannot be forwarded. {Message}", _messageOrEnvelope);
                    return;
                default:
                    SendUserMessage(target, _messageOrEnvelope);
                    break;
            }
        }

        public void Request(PID target, object message)
        {
            var messageEnvelope = new MessageEnvelope(message, Self);
            SendUserMessage(target, messageEnvelope);
        }

        public void Request(PID target, object message, PID? sender)
        {
            var messageEnvelope = new MessageEnvelope(message, sender);
            SendUserMessage(target, messageEnvelope);
        }

        public Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout)
            => RequestAsync<T>(target, message, new FutureProcess(System, timeout));

        public Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
            => RequestAsync<T>(target, message, new FutureProcess(System, cancellationToken));

        public Task<T> RequestAsync<T>(PID target, object message) =>
            RequestAsync<T>(target, message, new FutureProcess(System));

        public void ReenterAfter<T>(Task<T> target, Func<Task<T>, Task> action)
        {
            var msg = _messageOrEnvelope;
            var cont = new Continuation(() => action(target), msg);

            Task.Run(async () =>
                {
                    await target;
                    Self.SendSystemMessage(System, cont);
                }
                , CancellationToken.None);
        }

        public void ReenterAfter(Task target, Action action)
        {
            var msg = _messageOrEnvelope;

            var cont = new Continuation(
                () =>
                {
                    action();
                    return Task.CompletedTask;
                }, msg
            );

            target.ContinueWith(t => Self.SendSystemMessage(System, cont));
        }

        public Task Receive(MessageEnvelope envelope)
        {
            _messageOrEnvelope = envelope;
            return DefaultReceive();
        }

        public void Stop(PID pid)
        {
            var reff = System.ProcessRegistry.Get(pid);
            reff.Stop(pid);
        }

        public Task StopAsync(PID pid)
        {
            var future = new FutureProcess(System);

            pid.SendSystemMessage(System, new Watch(future.Pid));
            Stop(pid);

            return future.Task;
        }

        public void Poison(PID pid) => pid.SendUserMessage(System, PoisonPill.Instance);

        public Task PoisonAsync(PID pid)
        {
            var future = new FutureProcess(System);

            pid.SendSystemMessage(System, new Watch(future.Pid));
            Poison(pid);

            return future.Task;
        }

        public void EscalateFailure(Exception reason, object? message)
        {
            var failure = new Failure(Self, reason, EnsureExtras().RestartStatistics, message);
            Self.SendSystemMessage(System, SuspendMailbox.Instance);

            if (Parent == null)
            {
                HandleRootFailure(failure);
            }
            else
            {
                Parent.SendSystemMessage(System, failure);
            }
        }

        public Task InvokeSystemMessageAsync(object msg)
        {
            try
            {
                return msg switch
                {
                    Started s => InvokeUserMessageAsync(s),
                    Stop _ => InitiateStopAsync(),
                    Terminated t => HandleTerminatedAsync(t),
                    Watch w => HandleWatch(w),
                    Unwatch uw => HandleUnwatch(uw),
                    Failure f => HandleFailure(f),
                    Restart _ => HandleRestartAsync(),
                    SuspendMailbox _ => Task.CompletedTask,
                    ResumeMailbox _ => Task.CompletedTask,
                    Continuation cont => HandleContinuation(cont),
                    _ => HandleUnknownSystemMessage(msg)
                };
            }
            catch (Exception x)
            {
                Logger.LogError(x, "Error handling SystemMessage {Message}", msg);
                throw;
            }
        }

        private static Task HandleUnknownSystemMessage(object msg)
        {
            Logger.LogDebug("Unknown system message {Message}", msg);
            return Task.CompletedTask;
        }

        private Task HandleContinuation(Continuation cont)
        {
            _messageOrEnvelope = cont.Message;
            return cont.Action();
        }

        public Task InvokeUserMessageAsync(object msg)
        {
            if (_state == ContextState.Stopped)
            {
                //already stopped, send message to deadletter process
                System.DeadLetter.SendUserMessage(Self, msg);
                return Task.CompletedTask;
            }

            var influenceTimeout = true;

            if (ReceiveTimeout > TimeSpan.Zero)
            {
                var notInfluenceTimeout = msg is INotInfluenceReceiveTimeout;
                influenceTimeout = !notInfluenceTimeout;

                if (influenceTimeout)
                {
                    _extras?.StopReceiveTimeoutTimer();
                }
            }

            var res = ProcessMessageAsync(msg);

            if (ReceiveTimeout != TimeSpan.Zero && influenceTimeout)
            {
                //special handle non completed tasks that need to reset ReceiveTimout
                if (!res.IsCompleted)
                {
                    return res.ContinueWith(_ => _extras?.ResetReceiveTimeoutTimer(ReceiveTimeout));
                }

                _extras?.ResetReceiveTimeoutTimer(ReceiveTimeout);
            }

            return res;
        }

        public IImmutableSet<PID> Children => _extras?.Children ?? EmptyChildren;

        public void RestartChildren(Exception reason, params PID[] pids) =>
            pids.SendSystemMessage(new Restart(reason), System);

        public void StopChildren(params PID[] pids) => pids.SendSystemMessage(Proto.Stop.Instance, System);

        public void ResumeChildren(params PID[] pids) => pids.SendSystemMessage(ResumeMailbox.Instance, System);

        private ActorContextExtras EnsureExtras()
        {
            if (_extras == null)
            {
                var context = _props.ContextDecoratorChain?.Invoke(this) ?? this;
                _extras = new ActorContextExtras(context);
            }

            return _extras;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Task DefaultReceive() =>
            Message switch
            {
                PoisonPill _ => HandlePoisonPill(),
                _ => Actor!.ReceiveAsync(_props.ContextDecoratorChain != null ? EnsureExtras().Context : this)
            };

        private Task HandlePoisonPill()
        {
            Stop(Self);
            return Task.CompletedTask;
        }

        private Task ProcessMessageAsync(object msg)
        {
            //slow path, there is middleware, message must be wrapped in an envelop
            if (_props.ReceiverMiddlewareChain != null)
            {
                return _props.ReceiverMiddlewareChain(EnsureExtras().Context, MessageEnvelope.Wrap(msg));
            }

            if (_props.ContextDecoratorChain != null)
            {
                return EnsureExtras().Context.Receive(MessageEnvelope.Wrap(msg));
            }

            //fast path, 0 alloc invocation of actor receive
            _messageOrEnvelope = msg;
            return DefaultReceive();
        }

        private async Task<T> RequestAsync<T>(PID target, object message, FutureProcess future)
        {
            var messageEnvelope = new MessageEnvelope(message, future.Pid);
            SendUserMessage(target, messageEnvelope);
            var result = await future.Task;
            switch (result)
            {
                case DeadLetterResponse _:
                    throw new DeadLetterException(target);
                case null:
                case T _:
                    return (T) result!;
                default:
                    throw new InvalidOperationException(
                        $"Unexpected message. Was type {result?.GetType()} but expected {typeof(T)}"
                    );
            }
        }

        private void SendUserMessage(PID target, object message)
        {
            if (_props.SenderMiddlewareChain != null)
            {
                //slow path
                _props.SenderMiddlewareChain(EnsureExtras().Context, target, MessageEnvelope.Wrap(message));
            }
            else
            {
                //fast path, 0 alloc
                target.SendUserMessage(System, message);
            }
        }

        private IActor? IncarnateActor()
        {
            _state = ContextState.Alive;
            return _props.Producer();
        }

        private async Task HandleRestartAsync()
        {
            _state = ContextState.Restarting;
            CancelReceiveTimeout();
            await InvokeUserMessageAsync(Restarting.Instance);
            await StopAllChildren();
        }

        private Task HandleUnwatch(Unwatch uw)
        {
            _extras?.Unwatch(uw.Watcher);
            return Task.CompletedTask;
        }

        private Task HandleWatch(Watch w)
        {
            if (_state >= ContextState.Stopping)
            {
                w.Watcher.SendSystemMessage(System, Terminated.From(Self));
            }
            else
            {
                EnsureExtras().Watch(w.Watcher);
            }

            return Task.CompletedTask;
        }

        private Task HandleFailure(Failure msg)
        {
            switch (Actor)
            {
                case ISupervisorStrategy supervisor:
                    supervisor.HandleFailure(this, msg.Who, msg.RestartStatistics, msg.Reason, msg.Message);
                    break;
                default:
                    _props.SupervisorStrategy.HandleFailure(
                        this, msg.Who, msg.RestartStatistics, msg.Reason,
                        msg.Message
                    );
                    break;
            }

            return Task.CompletedTask;
        }

        private async Task HandleTerminatedAsync(Terminated msg)
        {
            _extras?.RemoveChild(msg.Who);
            await InvokeUserMessageAsync(msg);

            if (_state == ContextState.Stopping || _state == ContextState.Restarting)
            {
                await TryRestartOrStopAsync();
            }
        }

        private void HandleRootFailure(Failure failure)
            => Supervision.DefaultStrategy.HandleFailure(
                this, failure.Who, failure.RestartStatistics, failure.Reason,
                failure.Message
            );

        //Initiate stopping, not final
        private async Task InitiateStopAsync()
        {
            if (_state >= ContextState.Stopping)
            {
                //already stopping or stopped
                return;
            }

            _state = ContextState.Stopping;
            CancelReceiveTimeout();
            //this is intentional
            await InvokeUserMessageAsync(Stopping.Instance);
            await StopAllChildren();
        }

        private async Task StopAllChildren()
        {
            _extras?.Children?.Stop(System);

            await TryRestartOrStopAsync();
        }

        //intermediate stopping stage, waiting for children to stop
        private Task TryRestartOrStopAsync()
        {
            if (_extras?.Children.Count > 0)
            {
                return Task.CompletedTask;
            }

            CancelReceiveTimeout();

            return _state switch
            {
                ContextState.Restarting => RestartAsync(),
                ContextState.Stopping => FinalizeStopAsync(),
                _ => Task.CompletedTask
            };
        }

        //Last and final termination step
        private async Task FinalizeStopAsync()
        {
            System.ProcessRegistry.Remove(Self);
            //This is intentional
            await InvokeUserMessageAsync(Stopped.Instance);

            DisposeActorIfDisposable();

            //Notify watchers
            _extras?.Watchers.SendSystemMessage(Terminated.From(Self), System);

            //Notify parent
            Parent?.SendSystemMessage(System, Terminated.From(Self));

            _state = ContextState.Stopped;
        }

        private async Task RestartAsync()
        {
            DisposeActorIfDisposable();
            Actor = IncarnateActor();
            Self.SendSystemMessage(System, ResumeMailbox.Instance);

            await InvokeUserMessageAsync(Started.Instance);

            if (_extras?.Stash != null)
            {
                var currentStash = new Stack<object>(_extras.Stash);
                _extras.Stash.Clear();

                //TODO: what happens if we hit a failure here?
                while (currentStash.Any())
                {
                    var msg = currentStash.Pop();
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

        private void ReceiveTimeoutCallback(object state)
        {
            if (_extras?.ReceiveTimeoutTimer == null)
            {
                return;
            }

            CancelReceiveTimeout();
            Send(Self, Proto.ReceiveTimeout.Instance);
        }
    }
}