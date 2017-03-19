using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Proto
{
    public class NullLoggerFactory : ILoggerFactory
    {
        public static readonly NullLoggerFactory Instance = new NullLoggerFactory();

        public ILogger CreateLogger(string name)
        {
            return NullLogger.Instance;
        }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }
    }
}