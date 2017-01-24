// -----------------------------------------------------------------------
//  <copyright file="Context.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Proto
{
    public interface IMessageInvoker
    {
        Task InvokeSystemMessageAsync(SystemMessage msg);
        Task InvokeUserMessageAsync(object msg);
    }

    public interface IContext
    {
        PID Parent { get; }
        PID Self { get; }

        Props Props { get; }
        object Message { get; }
        PID Sender { get; }
        IActor Actor { get; }

        void Respond(object msg);
        PID[] Children();

        void Stash();
        void Receive(object message);
        Task NextAsync();
        PID Spawn(Props props);

        void Become(Receive receive);
        void BecomeStacked(Receive receive);
        void UnbecomeStacked();
        void Watch(PID pid);
        void Unwatch(PID who);
    }

    public class Context : IMessageInvoker, IContext, ISupervisor
    {
        private readonly Stack<Receive> _behavior;
        private HashSet<PID> _children;
        private object _message;
        private int _receiveIndex;
        private bool _restarting;
        private Stack<object> _stash;
        private bool _stopping;
        private HashSet<PID> _watchers;
        private HashSet<PID> _watching;
        private RestartStatistics _restartStatistics;

        public Context(Props props, PID parent)
        {
            Parent = parent;
            Props = props;
            _behavior = new Stack<Receive>();
            _behavior.Push(ActorReceive);

            IncarnateActor();
        }

        public PID[] Children()
        {
            return _children.ToArray();
        }

        public IActor Actor { get; private set; }
        public PID Parent { get; }
        public PID Self { get; internal set; }
        public Props Props { get; }
        
        public object Message
        {
            get
            {
                var r = _message as MessageSender;
                return r != null ? r.Message : _message;
            }
            private set { _message = value; }
        }

        public PID Sender => (_message as MessageSender)?.Sender;

        public void Stash()
        {
            if (_stash == null)
            {
                _stash = new Stack<object>();
            }
            _stash.Push(Message);
        }

        public void Receive(object message)
        {
            var i = _receiveIndex;
            var m = Message;

            _receiveIndex = 0;
            Message = message;
            NextAsync().Wait();

            _receiveIndex = i;
            Message = m;
        }

        public Task NextAsync()
        {
            Receive receive;
            if (_receiveIndex < Props.ReceivePlugins?.Length)
            {
                receive = Props.ReceivePlugins[_receiveIndex];
                _receiveIndex++;
            }
            else
            {
                var func = _behavior.Peek();
                receive = func;
            }

            return receive(this);
        }

        public void Respond(object msg)
        {
            Sender.Tell(msg);
        }

        public PID Spawn(Props props)
        {
            var id = ProcessRegistry.Instance.NextId();

            return SpawnNamed(props, id);
        }

        public void Become(Receive receive)
        {
            _behavior.Clear();
            _behavior.Push(receive);
        }

        public void BecomeStacked(Receive receive)
        {
            _behavior.Push(receive);
        }

        public void UnbecomeStacked()
        {
            if (_behavior.Count <= 1)
            {
                throw new Exception("Can not unbecome actor base behaviour");
            }
            _behavior.Pop();
        }

        public void Watch(PID who)
        {
            who.SendSystemMessage(new Watch(Self));
        }

        public void Unwatch(PID who)
        {
            who.SendSystemMessage(new Unwatch(Self));
        }

        public async Task InvokeSystemMessageAsync(SystemMessage msg)
        {
            try
            {
                switch (msg)
                {
                    case Started s:
                        await InvokeUserMessageAsync(s);
                        break;
                    case Stop _:
                        await HandleStopAsync();
                        break;
                    case Terminated t:
                        await HandleTerminatedAsync(t);
                        break;
                    case Watch w:
                        HandleWatch(w);
                        break;
                    case Unwatch uw:
                        HandleUnwatch(uw);
                        break;
                    case Failure f:
                        HandleFailure(f);
                        break;
                    case Restart r:
                        await HandleRestartAsync();
                        break;
                    case SuspendMailbox sm:
                        break;
                    case ResumeMailbox rm:
                        break;
                    default:
                        Console.WriteLine("Unknown system message {0}", msg);
                        break;
                }
            }
            catch (Exception x)
            {
                Console.WriteLine("Error handling SystemMessage {0}", x);
            }
        }

        public async Task InvokeUserMessageAsync(object msg)
        {
            try
            {
                _receiveIndex = 0;
                Message = msg;

                await NextAsync();
            }
            catch (Exception x)
            {
                if (_restartStatistics == null)
                {
                    _restartStatistics = new RestartStatistics(1, null);
                }
                var failure = new Failure(Self, x, _restartStatistics);
                if (Parent == null)
                {
                    HandleRootFailure(failure);
                }
                else
                {
                    Self.SendSystemMessage(SuspendMailbox.Instance);
                    Parent.SendSystemMessage(failure);
                }
            }
        }

        public void EscalateFailure(PID who, Exception reason)
        {
            Self.SendSystemMessage(SuspendMailbox.Instance);
            Parent.SendSystemMessage(new Failure(who, reason, _restartStatistics));
        }

        private void IncarnateActor()
        {
            _restarting = false;
            _stopping = false;
            Actor = Props.Producer();
            Become(ActorReceive);
        }

        private async Task HandleRestartAsync()
        {
            _stopping = false;
            _restarting = true;

            await InvokeUserMessageAsync(new Restarting());
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
            if (_watchers == null)
            {
                _watchers = new HashSet<PID>();
            }
            _watchers.Add(w.Watcher);
        }

        private void HandleFailure(Failure msg)
        {
            if (Actor is ISupervisorStrategy supervisor)
            {
                supervisor.HandleFailure(this, msg.Who, msg.RestartStatistics, msg.Reason);
                return;
            }
            Props.Supervisor.HandleFailure(this, msg.Who, msg.RestartStatistics, msg.Reason);
        }

        private async Task HandleTerminatedAsync(Terminated msg)
        {
            _children.Remove(msg.Who);
            _watching.Remove(msg.Who);
            await InvokeUserMessageAsync(msg);
            await TryRestartOrTerminateAsync();
        }

        private void HandleRootFailure(Failure failure)
        {
            Supervision.DefaultStrategy.HandleFailure(this, failure.Who, failure.RestartStatistics, failure.Reason);
        }

        private async Task HandleStopAsync()
        {
            _restarting = false;
            _stopping = true;
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
            if (_children?.Count > 0)
            {
                return;
            }

            if (_restarting)
            {
                await RestartAsync();
                return;
            }

            if (_stopping)
            {
                await StopAsync();
            }
        }

        private async Task StopAsync()
        {
            ProcessRegistry.Instance.Remove(Self);
            //This is intentional
            await InvokeUserMessageAsync(Stopped.Instance);
            //Notify watchers
        }

        private async Task RestartAsync()
        {
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

        private Task ActorReceive(IContext ctx)
        {
            return Actor.ReceiveAsync(ctx);
        }

        public PID SpawnNamed(Props props, string name)
        {
            string fullname;
            if (Parent != null)
            {
                fullname = Parent.Id + "/" + name;
            }
            else
            {
                fullname = name;
            }

            var pid = Proto.Actor.DefaultSpawner(fullname, props, Self);
            if (_children == null)
            {
                _children = new HashSet<PID>();
            }
            _children.Add(pid);
            Watch(pid);
            return pid;
        }
    }
}