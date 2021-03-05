// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster.PubSub
{
    public static class Extensions
    {
        public static async Task Publish(this Cluster cluster, string topic, object message)
        {
            _ = await cluster.RequestAsync<PublishResponse>(topic, "topic", new PublishRequest(), CancellationToken.None);
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