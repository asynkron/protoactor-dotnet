namespace Proto.Router.Routers
{
    internal class RandomPoolRouterConfig : PoolRouterConfig
    {
        public RandomPoolRouterConfig(int poolSize)
            : base(poolSize)
        {
        }

        public override RouterState CreateRouterState()
        {
            return new RandomRouterState();
        }
    }
}