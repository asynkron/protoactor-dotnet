namespace Proto.Router.Routers
{
    internal class BroadcastPoolRouterConfig : PoolRouterConfig
    {
        public BroadcastPoolRouterConfig(int poolSize)
            : base(poolSize)
        {
        }

        public override RouterState CreateRouterState()
        {
            return new BroadcastRouterState();
        }
    }
}