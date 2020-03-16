using System.Threading.Tasks;
namespace Proto.ActorExtensions.Tests
{
    public interface ISampleActor : IActor
    {

    }
    public class SampleActor : ISampleActor
    {
        public static bool Created;

        public SampleActor()
        {
            Created = true;
        }

        public Task ReceiveAsync(IContext context)
        {
            return Actor.Done;
        }
    }
}