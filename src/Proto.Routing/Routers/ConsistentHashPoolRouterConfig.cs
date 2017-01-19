namespace Proto.Routing.Routers
{
    internal class ConsistentHashPoolRouterConfig : PoolRouterConfig
    {
        public ConsistentHashPoolRouterConfig(int poolSize)
            : base(poolSize)
        {
        }

        public override RouterState CreateRouterState()
        {
            return new ConsistentHashRouterState();
        }
    }
}