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
        void InvokeSystemMessage(SystemMessage msg);
        Task InvokeUserMessageAsync(object msg);
    }

    public interface IContext
    {
        PID Parent { get; }
        PID Self { get; }

        Props Props { get; }
        object Message { get; }
        PID Sender { get; }

        void Respond(object msg);
        PID[] Children();

        void Stash();
        Task NextAsync();
        PID Spawn(Props props);

        void Become(ReceiveAsync receive);
        void BecomeStacked(ReceiveAsync receive);
        void UnbecomeStacked();
    }

    public class Context : IMessageInvoker, IContext, ISupervisor
    {
        private IActor _actor;
        private Stack<ReceiveAsync> _behavior;
        private HashSet<PID> _children;
        private object _message;
        private int _receiveIndex;
        private ReceiveAsync[] _receivePlugins;
        private bool _restarting;
        private Stack<object> _stash;
        private bool _stopping;
        private HashSet<PID> _watchers;
        private HashSet<PID> _watching;

        public Context(Props props, PID parent)
        {
            Parent = parent;
            Props = props;
            _behavior = new Stack<ReceiveAsync>();
            _behavior.Push(ActorReceiveAsync);

            IncarnateActor();
        }

        public PID[] Children()
        {
            return _children.ToArray();
        }

        public PID Parent { get; }
        public PID Self { get; internal set; }
        public Props Props { get; }

        public object Message
        {
            get
            {
                var r = _message as Request;
                return r != null ? r.Message : _message;
            }
            private set { _message = value; }
        }

        public PID Sender => (_message as Request)?.Sender;

        public void Stash()
        {
            if (_stash == null)
            {
                _stash = new Stack<object>();
            }
            _stash.Push(Message);
        }

        public Task NextAsync()
        {
            ReceiveAsync receive;
            if (_receiveIndex < _receivePlugins?.Length)
            {
                receive = _receivePlugins[_receiveIndex];
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
            var id = ProcessRegistry.Instance.GetAutoId();

            return SpawnNamed(props, id);
        }

        public void Become(ReceiveAsync receive)
        {
            _behavior.Clear();
            _behavior.Push(receive);
        }

        public void BecomeStacked(ReceiveAsync receive)
        {
            _behavior.Push(receive);
        }

        public void UnbecomeStacked()
        {
            if (_behavior.Count <= 1)
                throw new Exception("Can not unbecome actor base behaviour");
            _behavior.Pop();
        }

        public void InvokeSystemMessage(SystemMessage msg)
        {
            try
            {
                switch (msg)
                {
                    case Stop _:
                        HandleStop();
                        break;
                    case Terminated t:
                        HandleTerminated(t);
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
                        HandleRestart(r);
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
                var failure = new Failure(Self, x);
                if (Parent == null)
                {
                    HandleRootFailure(failure);
                }
                else
                {
                    Self.SendSystemMessage(new SuspendMailbox());
                    Parent.SendSystemMessage(failure);
                }
            }
        }

        public void EscalateFailure(PID who, Exception reason)
        {
            Self.SendSystemMessage(new SuspendMailbox());
            Parent.SendSystemMessage(new Failure(who, reason));
        }

        private void IncarnateActor()
        {
            _restarting = false;
            _stopping = false;
            _actor = Props.Producer();
            Become(ActorReceiveAsync);
        }

        private void HandleRestart(Restart r)
        {
            _stopping = false;
            _restarting = true;

            InvokeUserMessageAsync(new Restarting()).Wait();
            if (_children != null)
                foreach (var child in _children)
                    child.Stop();
            TryRestartOrTerminate();
        }

        private void HandleUnwatch(Unwatch uw)
        {
            if (_watchers != null)
                _watchers.Remove(uw.Watcher);
        }

        private void HandleWatch(Watch w)
        {
            if (_watchers == null)
                _watchers = new HashSet<PID>();
            _watchers.Add(w.Watcher);
        }

        private void HandleFailure(Failure msg)
        {
            Props.Supervisor.HandleFailure(this, msg.Who, msg.Reason);
        }

        private void HandleTerminated(Terminated msg)
        {
            _children.Remove(msg.Who);
            _watching.Remove(msg.Who);
            InvokeUserMessageAsync(msg).Wait();
            TryRestartOrTerminate();
        }

        private void HandleRootFailure(Failure failure)
        {
            Supervision.DefaultStrategy.HandleFailure(this, failure.Who, failure.Reason);
        }

        private void HandleStop()
        {
            _restarting = false;
            _stopping = true;
            //this is intentional
            InvokeUserMessageAsync(Stopping.Instance).Wait();
            if (_children != null)
                foreach (var child in _children)
                    child.Stop();
            TryRestartOrTerminate();
        }

        private void TryRestartOrTerminate()
        {
            if (_children != null)
                if (_children.Count > 0)
                    return;

            if (_restarting)
            {
                Restart();
                return;
            }

            if (_stopping)
                Stopped();
        }

        private void Stopped()
        {
            ProcessRegistry.Instance.Remove(Self);
            //This is intentional
            InvokeUserMessageAsync(Proto.Stopped.Instance).Wait();
            //Notify watchers
        }

        private void Restart()
        {
            IncarnateActor();
            Self.SendSystemMessage(new ResumeMailbox());

            InvokeUserMessageAsync(Started.Instance).Wait();
            if (_stash != null)
                while (_stash.Any())
                {
                    var msg = _stash.Pop();
                    InvokeUserMessageAsync(msg).Wait();
                }
        }

        private Task ActorReceiveAsync(IContext ctx)
        {
            return _actor.ReceiveAsync(ctx);
        }

        public PID SpawnNamed(Props props, string name)
        {
            string fullname;
            if (Parent != null)
                fullname = Parent.Id + "/" + name;
            else
                fullname = name;

            var pid = Actor.InternalSpawn(props, fullname, Self);
            if (_children == null)
                _children = new HashSet<PID>();
            _children.Add(pid);
            Watch(pid);
            return pid;
        }

        private void Watch(PID who)
        {
            who.SendSystemMessage(new Watch(Self));
        }
    }
}