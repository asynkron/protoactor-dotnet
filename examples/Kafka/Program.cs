using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Serialization;
using Newtonsoft.Json;
using Proto;
using Proto.Kafka;
using Proto.Mailbox;
using Proto.Router;

namespace Kafka
{
    class Program
    {
        private static string _brokerList = "localhost:9092";
        private static string _topic = "test1";

        static void Main(string[] args)
        {
            //SimpleExample();
            MulticastExample();
        }

        private static void MulticastExample()
        {
            Console.WriteLine("Multicast example: Message is broadcast to both a Kafka topic and another actor");
            var (producerPid, producerProcess) = StartKafkaProducerProcess(_brokerList, _topic);
            var consumer = StartKafkaConsumer(_brokerList, _topic);
            var targetActor = Actor.Spawn(Actor.FromFunc(c =>
            {
                if(!(c.Message is SystemMessage))
                    Console.WriteLine(c.Message);
                return Actor.Done;
            }));
            var router = Actor.Spawn(Router.NewBroadcastGroup(Actor.FromFunc(c => Actor.Done), targetActor, producerPid));
            using (producerProcess)
            using (consumer)
            {
                Console.WriteLine("Started. Press ENTER to send test message.");

                while (true)
                {
                    Console.ReadLine();
                    router.Tell(new { Message = "hello" });
                }
            }
        }

        private static void SimpleExample()
        {
            Console.WriteLine("Simple example: One actor sends a message to a Kafka topic");
            var (producerPid, producerProcess) = StartKafkaProducerProcess(_brokerList, _topic);
            var consumer = StartKafkaConsumer(_brokerList, _topic);
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