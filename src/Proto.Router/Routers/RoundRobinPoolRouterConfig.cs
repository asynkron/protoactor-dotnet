namespace Proto.Router.Routers
{
    internal class RoundRobinPoolRouterConfig : PoolRouterConfig
    {
        public RoundRobinPoolRouterConfig(int poolSize)
            : base(poolSize)
        {
        }

        public override RouterState CreateRouterState()
        {
            return new RoundRobinRouterState();
        }
    }
}