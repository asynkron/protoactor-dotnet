using Proto.Routing.Routers;

namespace Proto.Routing
{
    public static class Routing
    {
        public static Props NewBroadcastGroup(Props props, params PID[] routees)
        {
            return props.WithSpawner(Spawner(new BroadcastGroupRouterConfig(routees)));
        }

        public static Props NewConsistentHashGroup(Props props, params PID[] routees)
        {
            return props.WithSpawner(Spawner(new ConsistentHashGroupRouterConfig(routees)));
        }

        public static Props NewRandomGroup(Props props, params PID[] routees)
        {
            return props.WithSpawner(Spawner(new RandomGroupRouterConfig(routees)));
        }

        public static Props NewRoundRobinGroup(Props props, params PID[] routees)
        {
            return props.WithSpawner(Spawner(new RoundRobinGroupRouterConfig(routees)));
        }

        public static Props NewBroadcastPool(Props props, int poolSize)
        {
            return props.WithSpawner(Spawner(new BroadcastPoolRouterConfig(poolSize)));
        }

        public static Props NewConsistentHashPool(Props props, int poolSize)
        {
            return props.WithSpawner(Spawner(new ConsistentHashPoolRouterConfig(poolSize)));
        }

        public static Props NewRandomPool(Props props, int poolSize)
        {
            return props.WithSpawner(Spawner(new RandomPoolRouterConfig(poolSize)));
        }

        public static Props NewRoundRobinPool(Props props, int poolSize)
        {
            return props.WithSpawner(Spawner(new RoundRobinPoolRouterConfig(poolSize)));
        }

        public static Spawner Spawner(IRouterConfig config)
        {
            return (id, props, parent) =>
            {
                var routeeProps = props.WithSpawner(null);
                var routerState = config.CreateRouterState();

                var routerProps = Actor.FromProducer(() => new RouterActor(routeeProps, config, routerState));
                var routerId = ProcessRegistry.Instance.NextId();
                var router = Actor.DefaultSpawner(routerId, routerProps, parent);

                var reff = new RouterActorRef(router, routerState);
                var res = ProcessRegistry.Instance.TryAdd(routerId, reff);
                var pid = res.Item1;
                return pid;

            };
        }
    }
}
