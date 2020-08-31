namespace Proto
{
    public class EventStreamProcess : Process
    {
        public EventStreamProcess(ActorSystem system) : base(system)
        {
        }

        protected internal override void SendUserMessage(PID pid, object message)
        {
            var (msg, _, _) = MessageEnvelope.Unwrap(message);
            System.EventStream.Publish(msg);
        }

        protected internal override void SendSystemMessage(PID pid, object message)
        {
            //pass
        }
    }
}