// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster.PubSub
{
    public class Publisher
    {
        private string _topic;
        private Cluster _cluster;
        private List<Task> _tasks = new();

        public Publisher(Cluster cluster, string topic)
        {
            _cluster = cluster;
            _topic = topic;
        }
        
        public void Publish(object message)
        {
            var t = _cluster.RequestAsync<PublishResponse>(_topic, "topic", message, CancellationToken.None);
            _tasks.Add(t);
        }

        public async Task WhenAllPublished()
        {
            await Task.WhenAll(_tasks);
            _tasks.Clear();
        }
    }
    public static class Extensions
    {
        public static Publisher Publisher(this Cluster cluster, string topic)
        {
            return new Publisher(cluster, topic);
        }
        public static async Task Publish(this Cluster cluster, string topic, object message)
        {
            _ = await cluster.RequestAsync<PublishResponse>(topic, "topic", message, CancellationToken.None);
        }
        public static async Task Subscribe(this Cluster cluster,  string topic, string subscriberIdentity, string subscriberKind)
        {
            _ = await cluster.RequestAsync<SubscribeResponse>(topic, "topic", new SubscribeRequest()
                {
                    Subscriber = new SubscriberIdentity()
                    {
                        ClusterIdentity = new ClusterIdentity()
                        {
                            Identity = subscriberIdentity,
                            Kind = subscriberKind
                        }
                    }
                }, CancellationToken.None
            );
        }
        
        public static async Task Subscribe(this Cluster cluster,  string topic, PID subscriber)
        {
            _ = await cluster.RequestAsync<SubscribeResponse>(topic, "topic", new SubscribeRequest()
                {
                    Subscriber = new SubscriberIdentity
                    {
                        Pid = subscriber
                    }
                }, CancellationToken.None
            );
        }
        
    }
}