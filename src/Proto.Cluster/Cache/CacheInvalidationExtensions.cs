// -----------------------------------------------------------------------
// <copyright file="CacheInvalidationExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Cache
{
    static class CacheInvalidationExtensions
    {
        private static readonly ILogger Logger = Log.CreateLogger(nameof(CacheInvalidationExtensions));

        public static Props WithPidCacheInvalidation(this Props props)
            => props.WithContextDecorator(context => {
                    var cacheInvalidation = context.System.Extensions.Get<ClusterCacheInvalidation>();

                    if (cacheInvalidation is null)
                    {
                        Logger.LogWarning("ClusterCacheInvalidation extension is not registered");
                        return context;
                    }

                    return new CacheInvalidationContext(
                        context,
                        cacheInvalidation
                    );
                }
            );

        private class CacheInvalidationContext : IContext
        {
            private readonly IContext _context;
            private readonly ClusterCacheInvalidation _plugin;
            private Action<MessageEnvelope>? _callBack;

            public CacheInvalidationContext(IContext context, ClusterCacheInvalidation cacheInvalidation)
            {
                _context = context;
                _plugin = cacheInvalidation;
            }

            public PID? Parent => _context.Parent;
            public PID? Self => _context.Self;
            public PID? Sender => _context.Sender;
            public IActor? Actor => _context.Actor;
            public ActorSystem System => _context.System;

            public async Task Receive(MessageEnvelope envelope)
            {
                await _context.Receive(envelope);

                if (envelope.Message is ClusterInit init)
                {
                    _callBack = _plugin.ForActor(init.ClusterIdentity, Self!);
                }
                else
                {
                    _callBack?.Invoke(envelope);
                }
            }

            public MessageHeader Headers => _context.Headers;
            public object? Message => _context.Message;

            public void Send(PID target, object message) => _context.Send(target, message);

            public void Request(PID target, object message) => _context.Request(target, message);

            public void Request(PID target, object message, PID? sender) => _context.Request(target, message, sender);

            public Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout) => _context.RequestAsync<T>(target, message, timeout);

            public Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
                => _context.RequestAsync<T>(target, message, cancellationToken);

            public Task<T> RequestAsync<T>(PID target, object message) => _context.RequestAsync<T>(target, message);

            public PID Spawn(Props props) => _context.Spawn(props);

            public PID SpawnNamed(Props props, string name) => _context.SpawnNamed(props, name);

            public PID SpawnPrefix(Props props, string prefix) => _context.SpawnPrefix(props, prefix);

            public void Stop(PID pid) => _context.Stop(pid);

            public Task StopAsync(PID pid) => _context.StopAsync(pid);

            public void Poison(PID pid) => _context.Poison(pid);

            public Task PoisonAsync(PID pid) => _context.PoisonAsync(pid);

            public CancellationToken CancellationToken => _context.CancellationToken;
            public TimeSpan ReceiveTimeout => _context.ReceiveTimeout;
            public IReadOnlyCollection<PID> Children => _context.Children;

            public void Respond(object message) => _context.Respond(message);

            public void Stash() => _context.Stash();

            public void Watch(PID pid) => _context.Watch(pid);

            public void Unwatch(PID pid) => _context.Unwatch(pid);

            public void SetReceiveTimeout(TimeSpan duration) => _context.SetReceiveTimeout(duration);

            public void CancelReceiveTimeout() => _context.CancelReceiveTimeout();

            public void Forward(PID target) => _context.Forward(target);

            public void ReenterAfter<T>(Task<T> target, Func<Task<T>, Task> action) => _context.ReenterAfter(target, action);

            public void ReenterAfter(Task target, Action action) => _context.ReenterAfter(target, action);
        }

        public static Cluster EnablePidCacheInvalidation(this Cluster cluster)
        {
            _ = new ClusterCacheInvalidation(cluster);
            return cluster;
        }
    }
}