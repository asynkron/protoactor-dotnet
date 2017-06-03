using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Serialization;
using Newtonsoft.Json;
using Proto;
using Proto.Kafka;

namespace Kafka
{
    class Program
    {
        static void Main(string[] args)
        {
            var brokerList = "localhost:9092";
            var topic = "test1";
            var (producerPid, producerProcess) = StartKafkaProducerProcess(brokerList, topic);
            var consumer = StartKafkaConsumer(brokerList, topic);
            using (producerProcess)
            using (consumer)
            {
                var actor1 = Actor.Spawn(Actor.FromProducer(() => new MyActor(producerPid)));
                Console.WriteLine("Started. Press ENTER to send test message.");

                while (true)
                {
                    Console.ReadLine();
                    actor1.Tell(new {Message = "hello"});
                }
            }
        }

        private static (PID, KafkaProducerProcess) StartKafkaProducerProcess(string brokerList, string topic)
        {
            Func<object, string> valueSerializer = JsonConvert.SerializeObject;
            var p = new KafkaProducerProcess(brokerList, topic, valueSerializer);
            var (pid, ok) = ProcessRegistry.Instance.TryAdd(p);
            if (ok) return (pid, p);
            else throw new Exception("Failed to add KafkaProducer to ProcessRegistry.");
        }

        private static Consumer<string, string> StartKafkaConsumer(string brokerList, string topic)
        {
            var config = new Dictionary<string, object>
            {
                ["bootstrap.servers"] = brokerList,
                ["group.id"] = "testconsumer1"
            };
            var sz = new StringDeserializer(Encoding.UTF8);
            var consumer = new Consumer<string, string>(config, sz, sz);
            consumer.Subscribe(topic);
            consumer.OnMessage += (sender, message) =>
            {
                var dsz = JsonConvert.DeserializeObject(message.Value);
                Console.WriteLine($"Kafka consumer: topic:{message.Topic} partition:{message.Partition} offset:{message.Offset} value:'{dsz}'");
            };
            Task.Run(() =>
            {
                while (true)
                {
                    consumer.Poll(100);
                }
            });
            return consumer;
        }
    }
}