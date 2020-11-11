using System.Threading;

namespace Proto.Extensions
{
    public static class ActorSystemExtension
    {
       
    }

    public class ActorSystemExtensionId<T> where T : IActorSystemExtension
    {
        private static int _nextId;

        private static int GetNextId()
        {
            return Interlocked.Increment(ref _nextId);
        }
        
        private readonly int _id = GetNextId();

        public int Id => _id;
    }
}