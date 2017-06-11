using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Serialization;

namespace Proto.Kafka
{
    public class KafkaProducerProcess : Process, IDisposable
    {
        private readonly Producer<string, string> _producer;
        private readonly string _topic;
        private readonly Func<object, string> _keyConstructor;
        private readonly Func<object, string> _valueSerializer;

        public KafkaProducerProcess(
            string brokerList, 
            string topic, 
            Func<object, string> valueSerializer, 
            Func<object, string> keyConstructor = null, 
            IEnumerable<KeyValuePair<string, object>> configuration = null)
        {
            var config = new List<KeyValuePair<string, object>>();
            config.Add(new KeyValuePair<string, object>("bootstrap.servers", brokerList));
            if (configuration != null) config.AddRange(configuration);
            var sz = new StringSerializer(Encoding.UTF8);
            _producer = new Producer<string, string>(config, sz, sz);
            _topic = topic;
            _keyConstructor = keyConstructor;
            _valueSerializer = valueSerializer;
        }

        protected override async Task SendUserMessage(PID pid, object message)
        {
            var key = _keyConstructor?.Invoke(message);
            var value = _valueSerializer(message);
            var deliveryReport = await _producer.ProduceAsync(_topic, key, value);
            if(deliveryReport.Error.HasError)
                throw new Exception(deliveryReport.Error.Reason);
        }

        protected override Task SendSystemMessage(PID pid, object message)
        {
            return Task.FromResult(0);
        }

        public void Dispose()
        {
            _producer?.Dispose();
        }
    }
}