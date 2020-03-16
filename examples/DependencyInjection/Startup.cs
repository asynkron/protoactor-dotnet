using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
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
                props.RegisterProps<DIActor>(p => p.WithReceiverMiddleware(next => async (c, env) =>
                {
                    Console.WriteLine($"enter {env.Message.GetType().FullName}");
                    await next(c, env);
                    Console.WriteLine($"exit {env.Message.GetType().FullName}");
                }));
            });
            services.AddTransient<IActorManager, ActorManager>();
            services.AddTransient<IDIActor, DIActor>();
        }

        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            //attach logger to proto logging just in case
            Log.SetLoggerFactory(loggerFactory);

            //This is only for demo purposes done in the service initialization
            var actorManager = app.ApplicationServices.GetRequiredService<IActorManager>();
            actorManager.Activate();
            //never do this
            Thread.Sleep(TimeSpan.FromSeconds(2));
            //notice, there is no second creation!
            actorManager.Activate();
            Thread.Sleep(TimeSpan.FromSeconds(2));
        }
    }
}