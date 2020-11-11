using System.Threading;

namespace Proto.Extensions
{
    public static class ActorSystemExtension
    {
        private static int _nextId;

        internal static int GetNextId()
        {
            return Interlocked.Increment(ref _nextId);
        }
    }
}