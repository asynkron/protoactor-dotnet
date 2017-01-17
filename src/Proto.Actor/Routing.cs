// -----------------------------------------------------------------------
//  <copyright file="Routing.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    public interface IRouterConfig
    {
        void OnStarted(IContext context, Props props, RouterState router);

        RouterState CreateRouterState();
    }

    public interface IGroupRouterConfig : IRouterConfig
    {
    }

    public interface IPoolRouterConfig : IRouterConfig
    {
    }

    public abstract class RouterState
    {
        public abstract HashSet<PID> GetRoutees();
        public abstract void SetRoutees(HashSet<PID> routees);
        public abstract void RouteMessage(object message, PID sender);
    }

    public abstract class GroupRouter : IGroupRouterConfig
    {
        protected HashSet<PID> Routees;

        public virtual void OnStarted(IContext context, Props props, RouterState router)
        {
            foreach (var pid in Routees)
            {
                context.Watch(pid);
            }
            router.SetRoutees(Routees);
        }

        public abstract RouterState CreateRouterState();
    }


    public abstract class PoolRouter : IPoolRouterConfig
    {
        private readonly int _poolSize;

        protected PoolRouter(int poolSize)
        {
            _poolSize = poolSize;
        }

        public virtual void OnStarted(IContext context, Props props, RouterState router)
        {
            var routees = Enumerable.Range(0, _poolSize).Select(x => context.Spawn(props));
            router.SetRoutees(new HashSet<PID>(routees));
        }

        public abstract RouterState CreateRouterState();
    }

    public abstract class RouterManagementMessage
    {
    }

    public class RouterActorRef : ActorRef
    {
        private readonly PID _router;
        private readonly RouterState _state;

        public RouterActorRef(PID router, RouterState state)
        {
            _router = router;
            _state = state;
        }

        public override void SendUserMessage(PID pid, object message, PID sender)
        {
            if (message is RouterManagementMessage)
            {
                var router = ProcessRegistry.Instance.Get(_router);
                router.SendUserMessage(pid, message, sender);
            }
            else
            {
                _state.RouteMessage(message, sender);
            }
        }

        public override void SendSystemMessage(PID pid, SystemMessage sys)
        {
            _router.SendSystemMessage(sys);
        }
    }

    public class RouterActor : IActor
    {
        private readonly IRouterConfig _config;
        private readonly Props _routeeProps;
        private readonly RouterState _routerState;

        public RouterActor(Props routeeProps, IRouterConfig config, RouterState routerState)
        {
            _routeeProps = routeeProps;
            _config = config;
            _routerState = routerState;
        }

        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is Started)
            {
                _config.OnStarted(context, _routeeProps, _routerState);
                return Actor.Done;
            }
            if (context.Message is RouterAddRoutee addRoutee)
            {
                var r = _routerState.GetRoutees();
                if (r.Contains(addRoutee.PID))
                {
                    return Actor.Done;
                }
                context.Watch(addRoutee.PID);
                r.Add(addRoutee.PID);
                _routerState.SetRoutees(r);
                return Actor.Done;
            }
            if (context.Message is RouterRemoveRoutee removeRoutee)
            {
                var r = _routerState.GetRoutees();
                if (!r.Contains(removeRoutee.PID))
                {
                    return Actor.Done;
                }
                context.Unwatch(removeRoutee.PID);
                r.Remove(removeRoutee.PID);
                _routerState.SetRoutees(r);
                return Actor.Done;
            }
            if (context.Message is RouterBroadcastMessage broadcastMessage)
            {
                foreach (var routee in _routerState.GetRoutees())
                {
                    routee.Request(broadcastMessage.Message, context.Sender);
                }
                return Actor.Done;
            }
            if (context.Message is RouterGetRoutees)
            {
                var r = _routerState.GetRoutees().ToList();
                context.Sender.Tell(new RouterRoutees(r));
            }
            return Actor.Done;
        }
    }

    public class RouterRoutees
    {
        public RouterRoutees(List<PID> pids)
        {
            PIDs = pids;
        }

        public List<PID> PIDs { get; }
    }

    public class RouterGetRoutees : RouterManagementMessage
    {
    }

    public class RouterBroadcastMessage : RouterManagementMessage
    {
        public object Message { get; set; }
    }

    public class RouterRemoveRoutee : RouterManagementMessage
    {
        public PID PID { get; set; }
    }

    public class RouterAddRoutee : RouterManagementMessage
    {
        public PID PID { get; set; }
    }

    public class RandomPoolRouter : PoolRouter
    {
        public RandomPoolRouter(int poolSize)
            : base(poolSize)
        {
        }

        public override RouterState CreateRouterState()
        {
            return new RandomRouterState();
        }
    }

    public class RandomGroupRouter : GroupRouter
    {
        public RandomGroupRouter(params PID[] routees)
        {
            Routees = new HashSet<PID>(routees);
        }

        public override RouterState CreateRouterState()
        {
            return new RandomRouterState();
        }
    }

    public class RandomRouterState : RouterState
    {
        private readonly Random _random = new Random();
        private HashSet<PID> _routees;
        private PID[] _values;

        public override HashSet<PID> GetRoutees()
        {
            return _routees;
        }

        public override void SetRoutees(HashSet<PID> routees)
        {
            _routees = routees;
            _values = routees.ToArray();
        }

        public override void RouteMessage(object message, PID sender)
        {
            var i = _random.Next(_values.Length);
            var pid = _values[i];
            pid.Request(message, sender);
        }
    }

    public class RoundRobinPoolRouter : PoolRouter
    {
        public RoundRobinPoolRouter(int poolSize)
            : base(poolSize)
        {
        }

        public override RouterState CreateRouterState()
        {
            return new RoundRobinRouterState();
        }
    }

    public class RoundRobinGroupRouter : GroupRouter
    {
        public RoundRobinGroupRouter(params PID[] routees)
        {
            Routees = new HashSet<PID>(routees);
        }

        public override RouterState CreateRouterState()
        {
            return new RoundRobinRouterState();
        }
    }

    public class RoundRobinRouterState : RouterState
    {
        private int _currentIndex;
        private HashSet<PID> _routees;
        private List<PID> _values;

        public override HashSet<PID> GetRoutees()
        {
            return _routees;
        }

        public override void SetRoutees(HashSet<PID> routees)
        {
            _routees = routees;
            _values = routees.ToList();
        }

        public override void RouteMessage(object message, PID sender)
        {
            var i = _currentIndex % _values.Count;
            var pid = _values[i];
            Interlocked.Add(ref _currentIndex, 1);
            pid.Request(message, sender);
        }
    }

    public class ConsistentHashPoolRouter : PoolRouter
    {
        public ConsistentHashPoolRouter(int poolSize)
            : base(poolSize)
        {
        }

        public override RouterState CreateRouterState()
        {
            return new ConsistentHashRouterState();
        }
    }

    public class ConsistentHashGroupRouter : GroupRouter
    {
        public ConsistentHashGroupRouter(params PID[] routees)
        {
            Routees = new HashSet<PID>(routees);
        }

        public override RouterState CreateRouterState()
        {
            return new ConsistentHashRouterState();
        }
    }

    public class ConsistentHashRouterState : RouterState
    {
        private HashRing _hashRing;
        private Dictionary<string, PID> _routeeMap;


        public override HashSet<PID> GetRoutees()
        {
            return new HashSet<PID>(_routeeMap.Values);
        }

        public override void SetRoutees(HashSet<PID> routees)
        {
            _routeeMap = new Dictionary<string, PID>();
            var nodes = new List<string>();
            foreach (var pid in routees)
            {
                var nodeName = pid.Address + "@" + pid.Id;
                nodes.Add(nodeName);
                _routeeMap[nodeName] = pid;
            }
            _hashRing = new HashRing(nodes);
        }

        public override void RouteMessage(object message, PID sender)
        {
            if (message is IHashable hashable)
            {
                var key = hashable.HashBy();
                var node = _hashRing.GetNode(key);
                var routee = _routeeMap[node];
                routee.Request(message, sender);
            }
        }
    }

    public interface IHashable
    {
        string HashBy();
    }

    public class HashRing
    {
        private const int ReplicaCount = 100;
        private static readonly HashAlgorithm HashAlgorithm = MD5.Create();
        private readonly List<Tuple<uint, string>> _ring;

        public HashRing(IEnumerable<string> nodes)
        {
            _ring = nodes
                .SelectMany(n => Enumerable.Range(0, ReplicaCount).Select(i => new
                {
                    hashKey = i + n,
                    node = n
                }))
                .Select(a => Tuple.Create(Hash(a.hashKey), a.node))
                .OrderBy(t => t.Item1)
                .ToList();
        }

        public string GetNode(string key)
        {
            return (
                _ring.FirstOrDefault(t => t.Item1 > Hash(key))
                ?? _ring.First()
            ).Item2;
        }

        private static uint Hash(string s)
        {
            var digest = HashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(s));
            var hash = BitConverter.ToUInt32(digest, 0);
            return hash;
        }
    }

    public class BroadcastPoolRouter : PoolRouter
    {
        public BroadcastPoolRouter(int poolSize)
            : base(poolSize)
        {
        }

        public override RouterState CreateRouterState()
        {
            return new BroadcastRouterState();
        }
    }

    public class BroadcastGroupRouter : GroupRouter
    {
        public BroadcastGroupRouter(params PID[] routees)
        {
            Routees = new HashSet<PID>(routees);
        }

        public override RouterState CreateRouterState()
        {
            return new BroadcastRouterState();
        }
    }

    public class BroadcastRouterState : RouterState
    {
        private HashSet<PID> _routees;

        public override HashSet<PID> GetRoutees()
        {
            return _routees;
        }

        public override void SetRoutees(HashSet<PID> routees)
        {
            _routees = routees;
        }

        public override void RouteMessage(object message, PID sender)
        {
            foreach (var pid in _routees)
            {
                pid.Request(message, sender);
            }
        }
    }
}