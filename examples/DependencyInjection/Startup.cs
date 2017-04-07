using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Proto;

namespace DependencyInjection
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddProtoActor();
            services.AddTransient<IActorManager, ActorManager>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory,
            IApplicationLifetime appLifetime)
        {
            loggerFactory.AddConsole(LogLevel.Debug);
            //attach logger to proto logging just in case
            Proto.Log.SetLoggerFactory(loggerFactory);

            //This is only for demo purposes done in the service initialization
            app.ApplicationServices.GetRequiredService<IActorManager>().Activate();
            //never do this
            Thread.Sleep(TimeSpan.FromSeconds(2));
            //notice, there is no second creation!
            app.ApplicationServices.GetRequiredService<IActorManager>().Activate();
        }
    }
}