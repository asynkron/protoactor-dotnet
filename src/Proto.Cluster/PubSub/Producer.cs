// -----------------------------------------------------------------------
// <copyright file="Publisher.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster.PubSub
{
    public class Producer
    {
        private readonly Cluster _cluster;
        private readonly List<Task> _tasks = new();

        public Producer(Cluster cluster)
        {
            _cluster = cluster;
        }
        
        public Task ProduceAsync(string topic, object message)
        {
            var t = _cluster.RequestAsync<PublishResponse>(topic, "topic", message, CancellationToken.None);
            _tasks.Add(t);
            return t;
        }
        
        public void Produce(string topic, object message)
        {
            var t = _cluster.RequestAsync<PublishResponse>(topic, "topic", message, CancellationToken.None);
            _tasks.Add(t);
        }

        public async Task WhenAllPublished()
        {
            await Task.WhenAll(_tasks);
            _tasks.Clear();
        }
    }
}