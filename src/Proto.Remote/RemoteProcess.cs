// -----------------------------------------------------------------------
//   <copyright file="RemoteProcess.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Remote
{
    public class RemoteProcess : Process
    {
        private readonly EndpointManager _endpointManager;
        private readonly PID _pid;

        public RemoteProcess(ActorSystem system, EndpointManager endpointManager, PID pid) : base(system)
        {
            _endpointManager = endpointManager;
            _pid = pid;
        }

        protected override void SendUserMessage(PID _, object message) => Send(message);

        protected override void SendSystemMessage(PID _, object message) => Send(message);

        private void Send(object msg)
        {
            switch (msg)
            {
                case Watch w:
                    Watch(w);
                    break;
                case Unwatch uw:
                    Unwatch(uw);
                    break;
                default:
                    SendMessage(msg);
                    break;
            }
        }

        private void SendMessage(object msg)
        {
            _endpointManager.SendMessage(_pid, msg, -1);
        }

        private void Unwatch(Unwatch uw)
        {
            var ruw = new RemoteUnwatch(uw.Watcher, _pid);
            _endpointManager.RemoteUnwatch(ruw);
        }

        private void Watch(Watch w)
        {
            var rw = new RemoteWatch(w.Watcher, _pid);
            _endpointManager.RemoteWatch(rw);
        }
    }
}