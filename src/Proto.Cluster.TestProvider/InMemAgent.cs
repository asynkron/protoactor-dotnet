using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Proto.Cluster.Testing
{
    public class AgentServiceStatus
    {
        public string ID { get; set; }
        public DateTimeOffset TTL { get; set; }
        public bool Alive => DateTimeOffset.Now - TTL <= TimeSpan.FromSeconds(5);

        public string Host { get; set; }

        public int Port { get; set; }

        public string[] Kinds { get; set; }
        public string StatusValue { get; set; } //what goes here?
    }
    public sealed class InMemAgent
    {
        public event EventHandler StatusUpdate;

        private void OnStatusUpdate(EventArgs e) => StatusUpdate?.Invoke(this, e);
       

        private readonly ConcurrentDictionary<string,AgentServiceStatus> _services = new ConcurrentDictionary<string, AgentServiceStatus>();
        public  AgentServiceStatus[] GetServicesHealth()
        {
            return _services.Values.ToArray();
        }

        public void RegisterService(AgentServiceRegistration registration)
        {
            _services.TryAdd(registration.ID, new AgentServiceStatus
            {
                ID = registration.ID,
                TTL = DateTimeOffset.Now,
                Kinds = registration.Kinds,
                Host = registration.Address,
                Port = registration.Port,
            });
            OnStatusUpdate(EventArgs.Empty);
        }

        public void DeregisterService(string id)
        {
            _services.TryRemove(id,out _);
            OnStatusUpdate(EventArgs.Empty);
        }

        public void RefreshServiceTTL(string id)
        {
            //TODO: this is racy, but yolo for now
            if (_services.TryGetValue(id,out var service))
            {
                service.TTL = DateTimeOffset.Now;
            }
        }
    }
}