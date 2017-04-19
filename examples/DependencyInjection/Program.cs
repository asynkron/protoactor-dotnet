using System;
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

            public void Start<TContext>(IHttpApplication<TContext> application)
            {
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