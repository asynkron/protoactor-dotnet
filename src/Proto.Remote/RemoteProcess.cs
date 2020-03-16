// -----------------------------------------------------------------------
//   <copyright file="RemoteProcess.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Remote
{
    public class RemoteProcess : Process
    {
        private readonly PID _pid;
        private readonly EndpointManager _endpointManager;
        private readonly Remote _remote;

        public RemoteProcess(Remote remote, ActorSystem system, EndpointManager endpointManager, PID pid) : base(system)
        {
            _remote = remote;
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
                    {
                        var rw = new RemoteWatch(w.Watcher, _pid);
                        _endpointManager.RemoteWatch(rw);
                        break;
                    }
                case Unwatch uw:
                    {
                        var ruw = new RemoteUnwatch(uw.Watcher, _pid);
                        _endpointManager.RemoteUnwatch(ruw);
                        break;
                    }
                default:
                    _remote.SendMessage(_pid, msg, -1);
                    break;
            }
        }
    }
}