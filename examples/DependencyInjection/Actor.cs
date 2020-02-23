using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto;

namespace DependencyInjection
{
    public class DIActor : IActor
    {
        public class Ping
        {
            public Ping(string name) => Name = name;

            public string Name { get; }
        }

        private readonly ILogger _logger;

        public DIActor(ILogger<DIActor> logger)
        {
            _logger = logger;

            logger.LogWarning("Created DIActor");
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Ping p:
                {
                    _logger.LogInformation($"Ping! {p.Name} replying on eventstream");
                    EventStream.Instance.Publish(p);
                    break;
                }
            }
            return Task.CompletedTask;
        }
    }
}