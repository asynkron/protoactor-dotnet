using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Proto.Mailbox;
using System.Linq;
using System.Threading;

namespace Proto.Scheduling
{
    public class AlarmClockActor : IActor
    {
        public static PID InstancePID { get => _lazyInstance.Value; }

        static Lazy<PID> _lazyInstance = new Lazy<PID>(() =>
        {
            var pid = Actor.SpawnNamed(Actor.FromProducer(() => new AlarmClockActor()), nameof(AlarmClockActor));
            return pid;
        }, LazyThreadSafetyMode.ExecutionAndPublication);
        

        private AlarmClockActor() { }

        const string _tickMessage = "tickMessage";
        readonly SortedSet<DelayMessage> _messages = new SortedSet<DelayMessage>(new DelayMessageComparer());

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case DelayMessage delayMsg:
                    _messages.Add(delayMsg);
                    context.Self.Tell(_tickMessage);
                    break;

                case _tickMessage:
                    if (_messages.Count == 0) break;

                    var now = DateTime.UtcNow;
                    var msgsToSend = _messages.TakeWhile(msg => msg.Timeout < now).ToArray();
                    if (msgsToSend.Length != 0)
                    {
                        foreach (var msg in msgsToSend)
                            msg.Target.Tell(msg.Message);
                        _messages.RemoveWhere(msg => msg.Timeout < now);
                    }
                    else
                    {
                        await Task.Delay(1);
                    }
                    context.Self.Tell(_tickMessage);
                    break;
            }
        }
    }
}
