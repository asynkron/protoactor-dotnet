// -----------------------------------------------------------------------
//  <copyright file="RemoteProcess.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
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

        public override Task SendUserMessageAsync(PID pid, object message)
        {
            return SendAsync(pid, message);
        }

        public override Task SendSystemMessageAsync(PID pid, object message)
        {
            return SendAsync(pid, message);
        }

        private Task SendAsync(PID _, object msg)
        {
            if (msg is Watch w)
            {
                var rw = new RemoteWatch(w.Watcher, _pid);
                return Remote.EndpointManagerPid.SendAsync(rw);
            }
            else if (msg is Unwatch uw)
            {
                var ruw = new RemoteUnwatch(uw.Watcher, _pid);
                return Remote.EndpointManagerPid.SendAsync(ruw);
            }
            else
            {
                return SendRemoteMessageAsync(_pid, msg);
            }
        }

        public static Task SendRemoteMessageAsync(PID pid, object msg)
        {
            var (message, sender, _) = Proto.MessageEnvelope.Unwrap(msg);

            if (message is IMessage protoMessage)
            {
                var env = new RemoteDeliver(protoMessage, pid, sender);
                return Remote.EndpointManagerPid.SendAsync(env);
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