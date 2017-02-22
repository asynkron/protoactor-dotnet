namespace Proto.TestFixtures
{
    public class TestProcess : Process
    {
        public override void SendUserMessage(PID pid, object message, PID sender)
        {
        }

        public override void SendSystemMessage(PID pid, object message)
        {
        }
    }
}
