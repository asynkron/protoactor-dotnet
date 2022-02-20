// -----------------------------------------------------------------------
// <copyright file="ActorContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Diagnostics;
using Proto.Future;
using Proto.Mailbox;
using Proto.Metrics;

namespace Proto.Context
{
    public class ActorContext : IMessageInvoker, IContext, ISupervisor
    {
        private static readonly ILogger Logger = Log.CreateLogger<ActorContext>();
        private static readonly ImmutableHashSet<PID> EmptyChildren = ImmutableHashSet<PID>.Empty;
        private readonly IMailbox _mailbox;
        private readonly Props _props;
        private ActorContextExtras? _extras;
        private object? _messageOrEnvelope;
        private ContextState _state;
        private readonly KeyValuePair<string, object?>[] _metricTags = Array.Empty<KeyValuePair<string, object?>>();

        public ActorContext(ActorSystem system, Props props, PID? parent, PID self, IMailbox mailbox)
        {
            System = system;
            _props = props;
            _mailbox = mailbox;

            //Parents are implicitly watching the child
            //The parent is not part of the Watchers set
            Parent = parent;
            Self = self;

            Actor = IncarnateActor();

            if (System.Metrics.Enabled)
            {
                _metricTags = new KeyValuePair<string, object?>[]
                    {new("id", System.Id), new("address", System.Address), new("actortype", Actor.GetType().Name)};

                ActorMetrics.ActorSpawnCount.Add(1, _metricTags);
            }
        }

     

        public ActorSystem System { get; }
        public CancellationToken CancellationToken => EnsureExtras().CancellationTokenSource.Token;
        public CancellationTokenSource? CancellationTokenSource => _extras?.CancellationTokenSource;
        IReadOnlyCollection<PID> IContext.Children => Children;

        public IActor Actor { get; private set; }
        public PID? Parent { get; }
        public PID Self { get; }

        public object? Message => MessageEnvelope.UnwrapMessage(_messageOrEnvelope);

        public PID? Sender => MessageEnvelope.UnwrapSender(_messageOrEnvelope);

        public MessageHeader Headers => MessageEnvelope.UnwrapHeader(_messageOrEnvelope);

        public TimeSpan ReceiveTimeout { get; private set; }

        public void Stash()
        {
            if (_messageOrEnvelope is not null) EnsureExtras().Stash.Push(_messageOrEnvelope);
        }

        public void Respond(object message)
        {
            if (Sender is not null)
            {
                if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("{Self} Responding to {Sender} with message {Message}", Self, Sender, message);

                SendUserMessage(Sender, message);
            }
            else
                Logger.LogWarning("{Self} Tried to respond but sender is null, with message {Message}", Self, message);
        }

        public PID SpawnNamed(Props props, string name)
        {
            if (props.GuardianStrategy is not null)
                throw new ArgumentException("Props used to spawn child cannot have GuardianStrategy.");

            try
            {
                var pid = props.Spawn(System, $"{Self.Id}/{name}", Self);
                EnsureExtras().AddChild(pid);

                return pid;
            }
            catch (Exception x)
            {
                Logger.LogError(x, "{Self} Failed to spawn child actor {Name}", Self, name);
                throw;
            }
        }

        public void Watch(PID pid) => pid.SendSystemMessage(System, new Watch(Self));

        public void Unwatch(PID pid) => pid.SendSystemMessage(System, new Unwatch(Self));

        public void Set<T, TI>(TI obj) where TI : T
        {
            if (obj is null) throw new NullReferenceException(nameof(obj));

            EnsureExtras().Store.Add<T>(obj);
        }

        public void Remove<T>() => _extras?.Store.Remove<T>();

        public T? Get<T>() => (T?) _extras?.Store.Get<T>();

        public void SetReceiveTimeout(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "Duration must be greater than zero");

            if (duration == ReceiveTimeout) return;

            ReceiveTimeout = duration;

            var extras = EnsureExtras();
            extras.StopReceiveTimeoutTimer();

            if (extras.ReceiveTimeoutTimer is null)
            {
                extras.InitReceiveTimeoutTimer(
                    new Timer(ReceiveTimeoutCallback, null, ReceiveTimeout, ReceiveTimeout)
                );
            }
            else extras.ResetReceiveTimeoutTimer(ReceiveTimeout);
        }

        public void CancelReceiveTimeout()
        {
            if (_extras?.ReceiveTimeoutTimer is null) return;

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
                    Logger.LogWarning("Message is null");
                    return;
                case SystemMessage _:
                    Logger.LogWarning("SystemMessage cannot be forwarded. {Message}", _messageOrEnvelope);
                    return;
                default:
                    SendUserMessage(target, _messageOrEnvelope);
                    break;
            }
        }

        public void Request(PID target, object message, PID? sender)
        {
            var messageEnvelope = new MessageEnvelope(message, sender);
            SendUserMessage(target, messageEnvelope);
        }

        //why does this method exist here and not as an extension?
        //because DecoratorContexts needs to go this way if we want to intercept this method for the context
        public Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
            => SenderContextExtensions.RequestAsync<T>(this, target, message, cancellationToken);

        /// <summary>
        /// Calls the callback on token cancellation. If CancellationToken is non-cancellable, this is a noop.
        /// </summary>
        public void ReenterAfterCancellation(CancellationToken token, Action onCancelled)
        {
            if (token.IsCancellationRequested)
            {
                ReenterAfter(Task.CompletedTask, onCancelled);
                return;
            }

            // Noop
            if (!token.CanBeCanceled) return;

            var tcs = new TaskCompletionSource<bool>();
            var registration = token.Register(() => tcs.SetResult(true));

            // Ensures registration is disposed with the actor
            var inceptionRegistration = CancellationToken.Register(() => registration.Dispose());

            ReenterAfter(tcs.Task, () => {
                    inceptionRegistration.Dispose();
                    onCancelled();
                }
            );
        }

        public void ReenterAfter<T>(Task<T> target, Func<Task<T>, Task> action)
        {
            var msg = _messageOrEnvelope;
            var cont = new Continuation(() => action(target), msg, Actor);

            ScheduleContinuation(target, cont);
        }

        public void ReenterAfter(Task target, Action action)
        {
            var msg = _messageOrEnvelope;

            var cont = new Continuation(
                () => {
                    action();
                    return Task.CompletedTask;
                },
                msg,
                Actor);

            ScheduleContinuation(target, cont);
        }

        public Task Receive(MessageEnvelope envelope)
        {
            _messageOrEnvelope = envelope;
            return DefaultReceive();
        }

        public IFuture GetFuture() => System.Future.Get();

        public void EscalateFailure(Exception reason, object? message)
        {
            if (System.Config.DeveloperSupervisionLogging)
            {
                Console.WriteLine($"[Supervision] Actor {Self} : {Actor.GetType().Name} failed with message:{message} exception:{reason}");
                Logger.LogError(reason, "[Supervision] Actor {Self} : {ActorType} failed with message:{Message} exception:{Reason}", Self,
                    Actor.GetType().Name, message, reason
                );
            }

            ActorMetrics.ActorFailureCount.Add(1, _metricTags);
            var failure = new Failure(Self, reason, EnsureExtras().RestartStatistics, message);
            Self.SendSystemMessage(System, SuspendMailbox.Instance);

            if (Parent is null)
                HandleRootFailure(failure);
            else
                Parent.SendSystemMessage(System, failure);
        }

        public ValueTask InvokeSystemMessageAsync(object msg)
        {
            try
            {
                return msg switch
                {
                    Started s                       => InvokeUserMessageAsync(s),
                    Stop _                          => HandleStopAsync(),
                    Terminated t                    => HandleTerminatedAsync(t),
                    Watch w                         => HandleWatch(w),
                    Unwatch uw                      => HandleUnwatch(uw),
                    Failure f                       => HandleFailureAsync(f),
                    Restart                         => HandleRestartAsync(),
                    SuspendMailbox or ResumeMailbox => default,
                    Continuation cont               => HandleContinuation(cont),
                    ProcessDiagnosticsRequest pdr   => HandleProcessDiagnosticsRequest(pdr),
                    _                               => HandleUnknownSystemMessage(msg)
                };
            }
            catch (Exception x)
            {
                Logger.LogError(x, "Error handling SystemMessage {Message}", msg);
                throw;
            }
        }

        public ValueTask InvokeUserMessageAsync(object msg)
        {
            if (!System.Metrics.Enabled) return InternalInvokeUserMessageAsync(msg);

            return Await(this, msg, _metricTags);

            //static, don't create a closure
            static async ValueTask Await(ActorContext self, object msg, KeyValuePair<string, object?>[] metricTags)
            {
                if (self.System.Metrics.Enabled)
                {
                    ActorMetrics.ActorMailboxLength.Record(
                        self._mailbox.UserMessageCount,
                        metricTags
                    );
                }

                var sw = Stopwatch.StartNew();
                await self.InternalInvokeUserMessageAsync(msg);
                sw.Stop();

                if (self.System.Metrics.Enabled && metricTags.Length == 3)
                {
                    ActorMetrics.ActorMessageReceiveDuration.Record(sw.Elapsed.TotalSeconds,
                        metricTags[0], metricTags[1], metricTags[2],
                        new("messagetype", MessageEnvelope.UnwrapMessage(msg)?.GetType().Name ?? "{null}")
                    );
                }
            }
        }

        public IImmutableSet<PID> Children => _extras?.Children ?? EmptyChildren;

        public void RestartChildren(Exception reason, params PID[] pids) =>
            pids.SendSystemMessage(new Restart(reason), System);

        public void StopChildren(params PID[] pids) => pids.SendSystemMessage(Proto.Stop.Instance, System);

        public void ResumeChildren(params PID[] pids) => pids.SendSystemMessage(ResumeMailbox.Instance, System);

        private ValueTask HandleProcessDiagnosticsRequest(ProcessDiagnosticsRequest processDiagnosticsRequest)
        {
            var diagnosticsString = "ActorType:" + Actor.GetType().Name + "\n";

            if (Actor is IActorDiagnostics diagnosticsActor)
            {
                diagnosticsString += diagnosticsActor.GetDiagnosticsString();
            }
            else
            {
                var res = System.Config.DiagnosticsSerializer(Actor);
                diagnosticsString += res;
            }

            processDiagnosticsRequest.Result.SetResult(diagnosticsString);

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueTask InternalInvokeUserMessageAsync(object msg)
        {
            if (_state == ContextState.Stopped)
            {
                //already stopped, send message to deadletter process
                System.DeadLetter.SendUserMessage(Self, msg);
                return default;
            }

            var influenceReceiveTimeout = false;

            if (ReceiveTimeout > TimeSpan.Zero && MessageEnvelope.UnwrapMessage(msg) is not INotInfluenceReceiveTimeout)
            {
                influenceReceiveTimeout = true;
                _extras?.StopReceiveTimeoutTimer();
            }

            Task t;

            //slow path, there is middleware, message must be wrapped in an envelope
            if (_props.ReceiverMiddlewareChain is not null)
                t = _props.ReceiverMiddlewareChain(EnsureExtras().Context, MessageEnvelope.Wrap(msg));
            else
            {
                if (_props.ContextDecoratorChain is not null)
                    t = EnsureExtras().Context.Receive(MessageEnvelope.Wrap(msg));
                else
                {
                    _messageOrEnvelope = msg;
                    t = DefaultReceive();
                }

                //fast path, 0 alloc invocation of actor receive
            }

            if (t.IsCompletedSuccessfully)
            {
                if (influenceReceiveTimeout)
                {
                    _extras?.ResetReceiveTimeoutTimer(ReceiveTimeout);
                }

                return default;
            }

            return Await(this, t, influenceReceiveTimeout);

            //static, dont create closure
            static async ValueTask Await(ActorContext self, Task t, bool resetReceiveTimeout)
            {
                await t;

                if (resetReceiveTimeout)
                {
                    self._extras?.ResetReceiveTimeoutTimer(self.ReceiveTimeout);
                }
            }
        }

        public static ActorContext Setup(ActorSystem system, Props props, PID? parent, PID self, IMailbox mailbox) =>
            new(system, props, parent, self, mailbox);

        //Note to self, the message must be sent no-matter if the task failed or not.
        //do not mess this up by first awaiting and then sending on success only
        private void ScheduleContinuation(Task target, Continuation cont)
        {
            // We pass System.Shutdown to ContinueWith so that when the ActorSystem is shutdown,
            // continuations will not execute anymore.
            // ReSharper disable once MethodSupportsCancellation
            _ = target.ContinueWith(
                _ => Self.SendSystemMessage(System, cont),
                System.Shutdown);
        }

        private static ValueTask HandleUnknownSystemMessage(object msg)
        {
            //TODO: sounds like a pretty severe issue if we end up here? what todo?
            Logger.LogWarning("Unknown system message {Message}", msg);
            return default;
        }

        private async ValueTask HandleContinuation(Continuation cont)
        {
            // Don't execute the continuation if the actor instance changed.
            // Without this, Continuation's Action closure would execute with
            // an older Actor instance.
            if (cont.Actor == Actor ||
                cont.Actor == null)
            {
                _messageOrEnvelope = cont.Message;
                await cont.Action();
            }
            else
            {
                if (Logger.IsEnabled(LogLevel.Warning))
                    Logger.LogWarning(
                        "{Self} Dropping Continuation (ReenterAfter) of {Message}",
                        Self,
                        MessageEnvelope.UnwrapMessage(cont.Message));
            }
        }

        private ActorContextExtras EnsureExtras()
        {
            if (_extras is not null) return _extras;

            var context = _props.ContextDecoratorChain?.Invoke(this) ?? this;
            _extras = new ActorContextExtras(context);

            return _extras;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Task DefaultReceive() =>
            Message switch
            {
                PoisonPill               => HandlePoisonPill(),
                IAutoRespond autoRespond => HandleAutoRespond(autoRespond),
                _                        => Actor.ReceiveAsync(_props.ContextDecoratorChain is not null ? EnsureExtras().Context : this)
            };

        private Task HandleAutoRespond(IAutoRespond autoRespond)
        {
            // receive normally
            var res = Actor.ReceiveAsync(_props.ContextDecoratorChain is not null ? EnsureExtras().Context : this);
            //then respond automatically
            var response = autoRespond.GetAutoResponse(this);
            Respond(response);
            //return task from receive
            return res;
        }

        private Task HandlePoisonPill()
        {
            Stop(Self);
            return Task.CompletedTask;
        }

        private void SendUserMessage(PID target, object message)
        {
            if (_props.SenderMiddlewareChain is null)
            {
                //fast path, 0 alloc
                target.SendUserMessage(System, message);
            }
            else
            {
                //slow path
                _props.SenderMiddlewareChain(EnsureExtras().Context, target, MessageEnvelope.Wrap(message));
            }
        }

        private IActor IncarnateActor()
        {
            _state = ContextState.Alive;
            return _props.Producer(System, this);
        }

        private async ValueTask HandleRestartAsync()
        {
            _state = ContextState.Restarting;
            CancelReceiveTimeout();
            await InvokeUserMessageAsync(Restarting.Instance);
            await StopAllChildren();

            if (System.Metrics.Enabled)
                ActorMetrics.ActorRestartedCount.Add(1, _metricTags);
        }

        private ValueTask HandleUnwatch(Unwatch uw)
        {
            _extras?.Unwatch(uw.Watcher);
            return default;
        }

        private ValueTask HandleWatch(Watch w)
        {
            if (_state >= ContextState.Stopping)
                w.Watcher.SendSystemMessage(System, Terminated.From(Self, TerminatedReason.Stopped));
            else
                EnsureExtras().Watch(w.Watcher);

            return default;
        }

        private ValueTask HandleFailureAsync(Failure msg)
        {
            switch (Actor)
            {
                //TODO: add test for this
                // ReSharper disable once SuspiciousTypeConversion.Global
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

            return default;
        }

        // this will be triggered by the actors own Termination, _and_ terminating direct children, or Watchees
        private async ValueTask HandleTerminatedAsync(Terminated msg)
        {
            //In the case of a Watchee terminating, this will have no effect, except that the terminate message is
            //passed onto the user message Receive for user level handling
            _extras?.RemoveChild(msg.Who);
            await InvokeUserMessageAsync(msg);

            if (_state is ContextState.Stopping or ContextState.Restarting) await TryRestartOrStopAsync();
        }

        private void HandleRootFailure(Failure failure)
            => Supervision.DefaultStrategy.HandleFailure(
                this, failure.Who, failure.RestartStatistics, failure.Reason,
                failure.Message
            );

        //Initiate stopping, not final
        private ValueTask HandleStopAsync()
        {
            if (_state >= ContextState.Stopping)
            {
                //already stopping or stopped
                return default;
            }

            _state = ContextState.Stopping;
            CancelReceiveTimeout();

            return Await(this);

            static async ValueTask Await(ActorContext self)
            {
                await self.InvokeUserMessageAsync(Stopping.Instance);
                await self.StopAllChildren();
            }
        }

        private ValueTask StopAllChildren()
        {
            if (_extras != null)
            {
                foreach (var pid in _extras.Children)
                {
                    System.Root.Stop(pid);
                }
            }

            return TryRestartOrStopAsync();
        }

        //intermediate stopping stage, waiting for children to stop
        //this is directly triggered by StopAllChildren, or by Terminated messages from stopping children
        private ValueTask TryRestartOrStopAsync()
        {
            if (_extras?.Children.Count > 0) return default;

            CancelReceiveTimeout();

            //all children are now stopped, should we restart or stop ourselves?
            return _state switch
            {
                ContextState.Restarting => RestartAsync(),
                ContextState.Stopping   => FinalizeStopAsync(),
                _                       => default
            };
        }

        //Last and final termination step
        private async ValueTask FinalizeStopAsync()
        {
            System.ProcessRegistry.Remove(Self);
            //This is intentional
            await InvokeUserMessageAsync(Stopped.Instance);

            _extras?.Dispose();

            await DisposeActorIfDisposable();

            //Notify watchers
            _extras?.Watchers.SendSystemMessage(Terminated.From(Self, TerminatedReason.Stopped), System);

            //Notify parent
            Parent?.SendSystemMessage(System, Terminated.From(Self, TerminatedReason.Stopped));

            _state = ContextState.Stopped;
        }

        private async ValueTask RestartAsync()
        {
            await DisposeActorIfDisposable();
            Actor = IncarnateActor();
            Self.SendSystemMessage(System, ResumeMailbox.Instance);

            await InvokeUserMessageAsync(Started.Instance);

            if (_extras?.Stash is not null)
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

        private ValueTask DisposeActorIfDisposable()
        {
            switch (Actor)
            {
                case IAsyncDisposable asyncDisposableActor:
                    return asyncDisposableActor.DisposeAsync();
                case IDisposable disposableActor:
                    disposableActor.Dispose();
                    break;
            }

            return default;
        }

        private void ReceiveTimeoutCallback(object? state)
        {
            if (_extras?.ReceiveTimeoutTimer is null) return;

            SendUserMessage(Self, Proto.ReceiveTimeout.Instance);
        }

        public CapturedContext Capture() => new(MessageEnvelope.Wrap(_messageOrEnvelope!), this);

        public void Apply(CapturedContext capturedContext) => _messageOrEnvelope = capturedContext.MessageEnvelope;

        public void Stop(PID pid)
        {
            if (System.Metrics.Enabled)
                ActorMetrics.ActorStoppedCount.Add(1, _metricTags);

            pid.Stop(System);
        }

        public Task StopAsync(PID pid)
        {
            var future = System.Future.Get();

            pid.SendSystemMessage(System, new Watch(future.Pid));
            Stop(pid);
            return future.Task;
        }

        public void Poison(PID pid) => pid.SendUserMessage(System, PoisonPill.Instance);

        public Task PoisonAsync(PID pid)
        {
            var future = System.Future.Get();

            pid.SendSystemMessage(System, new Watch(future.Pid));
            Poison(pid);
            return future.Task;
        }
    }
}