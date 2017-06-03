using System;
using System.Threading.Tasks;
using Proto;

namespace Kafka
{
    internal class MyActor : IActor
    {
        private readonly PID _kafkaProducer;

        public MyActor(PID kafkaProducer)
        {
            _kafkaProducer = kafkaProducer;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    break;
                default:
                    Console.WriteLine($"MyActor: Got message {context.Message}, sending to Kafka producer");
                    _kafkaProducer.Tell(context.Message);
                    break;
            }
            return Actor.Done;
        }
    }
}