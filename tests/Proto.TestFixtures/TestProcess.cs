namespace Proto.TestFixtures
{
    public class TestProcess : Process
    {
        protected override void SendUserMessage(PID pid, object message)
        {
        }

        protected override void SendSystemMessage(PID pid, object message)
        {
        }
    }
}
