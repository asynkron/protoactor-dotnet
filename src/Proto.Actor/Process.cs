// -----------------------------------------------------------------------
//  <copyright file="ActorRef.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

namespace Proto
{
    public partial class PID
    {
        public PID(string address, string id)
        {
            Address = address;
            Id = id;
        }

        internal Process Ref { get; set; }

        public void Tell(object message)
        {
            var reff = Ref ?? ProcessRegistry.Instance.Get(this);
            reff.SendUserMessage(this, message, null);
        }

        public void SendSystemMessage(SystemMessage sys)
        {
            var reff = Ref ?? ProcessRegistry.Instance.Get(this);
            reff.SendSystemMessage(this, sys);
        }

        public void Request(object message, PID sender)
        {
            var reff = Ref ?? ProcessRegistry.Instance.Get(this);
            reff.SendUserMessage(this, message, sender);
        }

        public Task<T> RequestAsync<T>(object message)
        {
            var tsc = new TaskCompletionSource<T>();
            var p = Actor.FromProducer(() => new FutureActor<T>(tsc));
            var fpid = Actor.Spawn(p);
            Tell(new Request(message, fpid));
            return tsc.Task;
        }

        public void Stop()
        {
            var reff = ProcessRegistry.Instance.Get(this);
            reff.Stop(this);
        }
    }

    public class Request
    {
        public Request(object message, PID sender)
        {
            Message = message;
            Sender = sender;
        }

        public object Message { get; }
        public PID Sender { get; }
    }

    public abstract class Process
    {
        public abstract void SendUserMessage(PID pid, object message, PID sender);

        public void Stop(PID pid)
        {
            SendSystemMessage(pid, new Stop());
        }

        public abstract void SendSystemMessage(PID pid, SystemMessage sys);
    }

    public class LocalActorRef : Process
    {
        public LocalActorRef(IMailbox mailbox)
        {
            Mailbox = mailbox;
        }

        public IMailbox Mailbox { get; }

        public override void SendUserMessage(PID pid, object message, PID sender)
        {
            if (sender != null)
            {
                Mailbox.PostUserMessage(new Request(message, sender));
                return;
            }

            Mailbox.PostUserMessage(message);
        }

        public override void SendSystemMessage(PID pid, SystemMessage sys)
        {
            Mailbox.PostSystemMessage(sys);
        }
    }
}