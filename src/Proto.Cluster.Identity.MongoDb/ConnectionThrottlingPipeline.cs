using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace Proto.Cluster.Identity.MongoDb
{
    public static class ConnectionThrottlingPipeline
    {
        private static Semaphore openConnectionSemaphore = null!;

        public static void Initialize(IMongoClient client)
            => openConnectionSemaphore = new Semaphore(
                client.Settings.MaxConnectionPoolSize / 2,
                client.Settings.MaxConnectionPoolSize / 2
            );

        public static async Task<T> AddRequest<T>(Task<T> task) {
            openConnectionSemaphore.WaitOne();

            try {
                var result = await task;
                return result;
            }
            finally {
                openConnectionSemaphore.Release();
            }
        }
        
        public static async Task AddRequest(Task task) {
            openConnectionSemaphore.WaitOne();

            try {
                await task;
            }
            finally {
                openConnectionSemaphore.Release();
            }
        }
    }
}