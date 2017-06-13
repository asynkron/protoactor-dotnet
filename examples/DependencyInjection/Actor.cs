using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto;

namespace DependencyInjection
{
    public class DIActor : IActor
    {
        public class Ping
        {
            public Ping(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        private readonly ILogger logger;
        private readonly EventStream<Ping> eventStream;

        public DIActor(ILogger<DIActor> logger, EventStream<Ping> eventStream)
        {
            this.logger = logger;
            this.eventStream = eventStream;

            logger.LogWarning("Created DIActor");
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Ping p:
                {
                    logger.LogInformation($"Ping! {p.Name} replying on eventstream");
                    return eventStream.PublishAsync(p);
                }
            }
            return Actor.Done;
        }
    }
}