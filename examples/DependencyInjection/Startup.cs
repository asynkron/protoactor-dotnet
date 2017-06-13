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
            services.AddProtoActor(props =>
            {
                //attached console tracing
                props.RegisterProps<DIActor>(p => p.WithReceiveMiddleware(next => async c =>
                {
                    Console.WriteLine($"enter {c.Actor.GetType().FullName} {c.Message.GetType().FullName}");
                    await next(c);
                    Console.WriteLine($"exit {c.Actor.GetType().FullName} {c.Message.GetType().FullName}");
                }));
            });
            services.AddTransient<IActorManager, ActorManager>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory,
            IApplicationLifetime appLifetime)
        {
            loggerFactory.AddConsole(LogLevel.Debug);
            //attach logger to proto logging just in case
            Proto.Log.SetLoggerFactory(loggerFactory);

            //This is only for demo purposes done in the service initialization
            var actorManager = app.ApplicationServices.GetRequiredService<IActorManager>();
            actorManager.ActivateAsync().Wait();
            //never do this
            Thread.Sleep(TimeSpan.FromSeconds(2));
            //notice, there is no second creation!
            actorManager.ActivateAsync().Wait();
        }
    }
}