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
            object message;
            var endpoint = _endpointManager.GetEndpoint(_pid.Address);
            switch (msg)
            {
                case Watch w:
                    if (endpoint is null)
                    {
                        System.Root.Send(w.Watcher, new Terminated { AddressTerminated = true, Who = _pid });
                        return;
                    }
                    message = new RemoteWatch(w.Watcher, _pid);
                    break;
                case Unwatch uw:
                    if (endpoint is null) return;
                    message = new RemoteUnwatch(uw.Watcher, _pid);
                    break;
                default:
                    var (m, sender, header) = Proto.MessageEnvelope.Unwrap(msg);
                    if (endpoint is null)
                    {
                        System.EventStream.Publish(new DeadLetterEvent(_pid, m, sender));
                        return;
                    }
                    message = new RemoteDeliver(header!, m, _pid, sender!, -1);
                    break;
            }
            System.Root.Send(endpoint, message);
        }
    }
}