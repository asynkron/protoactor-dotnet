using System.Threading.Tasks;

namespace Proto.TestFixtures
{
    public class TestProcess : Process
    {
        protected override Task SendUserMessageAsync(PID pid, object message)
        {
            return Actor.Done;
        }

        protected override Task SendSystemMessageAsync(PID pid, object message)
        {
            return Actor.Done;
        }
    }
}
