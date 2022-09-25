namespace Proto.Cluster.Dashboard;

public static class Extensions
{
    public static void AddProtoActorDashboard(this IServiceCollection services, ActorSystem system)
    {
        services.AddSingleton(system);
        services.AddSingleton(system.Cluster());
    }

    public static void AddProtoActorDashboard(this IServiceCollection services, ActorSystem system,
        DashboardSettings settings)
    {
        services.AddSingleton(system);
        services.AddSingleton(system.Cluster());
        services.AddSingleton(settings);
    }

    public static void AddProtoActorDashboard(this IServiceCollection services, DashboardSettings settings) =>
        services.AddSingleton(settings);
}