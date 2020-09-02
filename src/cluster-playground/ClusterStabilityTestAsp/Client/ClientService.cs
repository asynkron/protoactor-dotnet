using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Proto;
using Proto.Cluster;
using Messages;
using Microsoft.Extensions.Logging;
using System.Threading;
using Polly;
using System.Collections.Generic;

namespace Client
{
    public class ClientService : IHostedService
    {
        private readonly ActorSystem _actorSystem;
        private readonly Grains _grains;
        private readonly ILogger<ClientService> _logger;
        private readonly IHostApplicationLifetime _appLifetime;

        public ClientService(
            ActorSystem actorSystem,
            Grains grains,
            ILogger<ClientService> logger,
            IHostApplicationLifetime appLifetime)
        {
            _actorSystem = actorSystem;
            _grains = grains;
            _logger = logger;
            _appLifetime = appLifetime;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStarted.Register(OnStarted);
            return Task.CompletedTask;
        }

        private void OnStarted()
        {
            _ = Task.Run(async () =>
                {

                    try
                    {
                        await Task.Delay(2000);
                        _logger.LogCritical("Starting to send !");

                        var policy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(i => TimeSpan.FromMilliseconds(1000), (e, t) =>
                        {
                            _logger.LogError(e, e.Message);
                        });
                        var n = 1_000_000;
                        var tasks = new List<Task>();
                        for (var i = 0; i < n; i++)
                        {
                            tasks.Add(policy.ExecuteAsync(() =>
                                // _grains.HelloGrain("name" + (i % 2000)).SayHello(new HelloRequest())
                                _actorSystem.RequestAsync<HelloResponse>("name" + (i % 20000), "HelloActor", new HelloRequest(), new CancellationTokenSource(2000).Token)
                            ));
                            if (tasks.Count % 1000 == 0)
                            {
                                Task.WaitAll(tasks.ToArray());
                                tasks.Clear();
                            }
                        }
                        Task.WaitAll(tasks.ToArray());
                        _logger.LogCritical("Done!");
                        await Task.Delay(20_000);
                        _appLifetime.StopApplication();
                    }
                    catch (System.Exception e)
                    {
                        _logger.LogError(e, e.Message);
                        throw;
                    }
                }
            );
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}