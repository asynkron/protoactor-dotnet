using Microsoft.Extensions.Logging;
using Proto.Extensions;

namespace Proto.Logging
{
    public class LogExtension : IActorSystemExtension<LogExtension>
    {
        public LogExtension(ILoggerFactory loggerFactory)
        {
            LoggerFactory = loggerFactory;
        }
        
        public ILoggerFactory LoggerFactory { get; }
    }

    public static class LogExtensionExtensions
    {
        public static ILoggerFactory LoggerFactory(this ActorSystem actorSystem) => 
            actorSystem.Extensions.Get<LogExtension>().LoggerFactory;
    }
}