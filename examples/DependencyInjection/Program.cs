using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;

namespace DependencyInjection
{
    class Program
    {
        public class NullServer : IServer
        {
            public void Dispose()
            {
            }

            public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public IFeatureCollection Features => new FeatureCollection();
        }

        static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseServer(new NullServer())
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}