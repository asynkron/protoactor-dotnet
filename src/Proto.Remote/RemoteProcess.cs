// -----------------------------------------------------------------------
//  <copyright file="RemoteProcess.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Google.Protobuf;

namespace Proto.Remote
{
    public class RemoteProcess : Process
    {
        private readonly PID _pid;

        public RemoteProcess(PID pid)
        {
            _pid = pid;
        }

        protected override void SendUserMessage(PID pid, object message)
        {
            Send(pid, message);
        }

        protected override void SendSystemMessage(PID pid, object message)
        {
            Send(pid, message);
        }

        private void Send(PID _, object msg)
        {
            if (msg is Watch w)
            {
                var rw = new RemoteWatch(w.Watcher, _pid);
                Remote.EndpointManagerPid.Tell(rw);
            }
            else if (msg is Unwatch uw)
            {
                var ruw = new RemoteUnwatch(uw.Watcher, _pid);
                Remote.EndpointManagerPid.Tell(ruw);
            }
            else
            {
                SendRemoteMessage(_pid, msg);
            }
        }

        public static void SendRemoteMessage(PID pid, object msg)
        {
            var (message, sender, _) = Proto.MessageEnvelope.Unwrap(msg);

            if (message is IMessage protoMessage)
            {
                var env = new RemoteDeliver(protoMessage, pid, sender);
                Remote.EndpointManagerPid.Tell(env);
            }
            else
            {
                throw new NotSupportedException("Non protobuf message");
            }
        }
    }

    public class RemoteDeliver
    {
        public RemoteDeliver(IMessage message, PID target,PID sender)
        {
            Message = message;
            Target = target;
            Sender = sender;
        }
        public IMessage Message { get;  }
        public PID Target { get;  }
        public PID Sender { get;  }
    }
}