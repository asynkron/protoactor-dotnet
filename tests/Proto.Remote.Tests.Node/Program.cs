using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Proto.Remote.Tests.Node
{
    class Program
    {
        public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

        private static IHostBuilder CreateHostBuilder(string[] args)
            => Host
                .CreateDefaultBuilder(args)
                .ConfigureLogging(
                    logging => logging
                        .SetMinimumLevel(LogLevel.Debug)
                        .AddFilter("System", LogLevel.Warning)
                        .AddConsole()
                )
                .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
    }
}