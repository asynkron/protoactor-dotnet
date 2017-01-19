namespace Proto.Routing.Routers
{
    public interface IRouterConfig
    {
        void OnStarted(IContext context, Props props, RouterState router);

        RouterState CreateRouterState();
    }
}