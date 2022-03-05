namespace Proto.Cluster.Dashboard;

public static  class Extensions
{
    public static void AddProtoActorDashboard(this IServiceCollection services, ActorSystem system)
    {
        services.AddSingleton(system);
        services.AddSingleton(system.Cluster());
    }
}