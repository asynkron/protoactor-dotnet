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
        private PID? _endpoint;

        public RemoteProcess(ActorSystem system, EndpointManager endpointManager, PID pid) : base(system)
        {
            _endpointManager = endpointManager;
            _pid = pid;
        }

        protected internal override void SendUserMessage(PID _, object message) => Send(message);

        protected internal override void SendSystemMessage(PID _, object message) => Send(message);

        private void Send(object msg)
        {
            object message;
            _endpoint ??= _endpointManager.GetEndpoint(_pid.Address);

            switch (msg)
            {
                case Watch w:
                    if (_endpoint is null)
                    {
                        System.Root.Send(w.Watcher, new Terminated {Why = TerminatedReason.AddressTerminated, Who = _pid});
                        return;
                    }

                    message = new RemoteWatch(w.Watcher, _pid);
                    break;
                case Unwatch uw:
                    if (_endpoint is null) return;

                    message = new RemoteUnwatch(uw.Watcher, _pid);
                    break;
                default:
                    var (m, sender, header) = Proto.MessageEnvelope.Unwrap(msg);

                    if (_endpoint is null)
                    {
                        System.EventStream.Publish(new DeadLetterEvent(_pid, m, sender));
                        return;
                    }

                    message = new RemoteDeliver(header!, m, _pid, sender!);
                    break;
            }

            System.Root.Send(_endpoint, message);
        }
    }
}