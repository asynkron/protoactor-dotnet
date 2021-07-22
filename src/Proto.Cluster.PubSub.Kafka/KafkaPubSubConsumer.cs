using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.PubSub.Kafka
{
    [PublicAPI]
    public class KafkaPubSubConsumer<TMessage> : IPubSubConsumer
    {
        private readonly int _batchSize;

        private readonly string _topic;
        private readonly ILogger _log;
        private readonly Func<ConsumeResult<Ignore, byte[]>, TMessage> _deserializer;
        private readonly Func<TMessage, bool> _predicate;
        private readonly Cluster _cluster;
        private readonly ConsumerConfig _config;

        protected KafkaPubSubConsumer(Cluster cluster, ILogger logger, ConsumerConfig config, string topic, int batchSize, Func<ConsumeResult<Ignore, byte[]>, TMessage> deserializer, Func<TMessage,bool> predicate)
        {
            _cluster = cluster;
            _config = config;
            _log = logger;
            _batchSize = batchSize;
            _topic = topic;
            _deserializer = deserializer;
            _predicate = predicate;
        }
        
        public Task StartAsync(CancellationToken ct)
        {
            _ = Task.Run(() => StartConsumer(ct), ct);
            return Task.CompletedTask;
        }

        private async Task StartConsumer(CancellationToken ct)
        {
            var producer = _cluster.Producer(_topic);
            var builder = new ConsumerBuilder<Ignore, byte[]>(_config);
            var consumer = builder.Build();
            try
            {
                consumer.Subscribe(_topic);

                while (!ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                    var messages = new List<TMessage>();
                    var results = new List<ConsumeResult<Ignore, byte[]>>();

                    for (var i = 0; i < _batchSize; i++)
                    {
                        ct.ThrowIfCancellationRequested();

                        try
                        {
                            var consumeResult = consumer!.Consume(50);

                            if (consumeResult.IsPartitionEOF)
                            {
                                break;
                            }
                            
                            var msg = _deserializer(consumeResult);

                            if (!_predicate(msg)) continue;

                            results.Add(consumeResult);
                            messages.Add(msg);
                        }
                        catch (KafkaException e)
                        {
                            _log.LogError(e, "Commit error");
                            consumer.Close();
                            throw;
                        }
                        catch (OperationCanceledException)
                        {
                            _log.LogInformation("Closing consumer");
                            consumer.Close();
                            throw;
                        }
                    }

                    if (!messages.Any()) continue;

                    await ForwardToPubSub(messages, producer, results, consumer);
                }
            }
            catch (Exception x)
            {
                _log.LogError(x, "Consumer crashed");
                throw;
            }
            finally
            {
                _log.LogInformation("Consumer stopped");
                consumer.Close();
                consumer.Dispose();
            }
        }

        private static async Task ForwardToPubSub(List<TMessage> messages, Producer producer, List<ConsumeResult<Ignore, byte[]>> results, IConsumer<Ignore, byte[]> consumer)
        {
            //forward messages to proto PubSub
            var tasks = messages.Select(msg => producer.ProduceAsync(msg)).ToList();
            await Task.WhenAll(tasks);

            var offsets = results
                .GroupBy(consumeResult => (consumeResult.Topic, consumeResult.Partition))
                .Select(group =>
                    new TopicPartitionOffset(@group.Key.Topic, @group.Key.Partition, new Offset(@group.Max(o => o.Offset.Value)))
                )
                .ToList();

            consumer.Commit(offsets);
        }
    }
}