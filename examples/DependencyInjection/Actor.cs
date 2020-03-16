using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto;

namespace DependencyInjection
{
    public interface IDIActor : IActor
    {
    }

    public class DIActor : IDIActor
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
        private readonly ActorSystem system;

        public DIActor(ILogger<DIActor> logger, ActorSystem system)
        {
            this.logger = logger;
            this.system = system;
            logger.LogWarning("Created DIActor");
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Ping p:
                    {
                        logger.LogInformation($"Ping! {p.Name} replying on eventstream");
                        system.EventStream.Publish(p);
                        break;
                    }
            }
            return Task.CompletedTask;
        }
    }
}