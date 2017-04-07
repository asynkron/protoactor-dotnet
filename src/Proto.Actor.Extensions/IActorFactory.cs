namespace Proto
{
    public interface IActorFactory
    {
        PID GetActor<T>(string id = null, string address = null, IContext parent = null)
            where T : IActor;

        PID GetActor<T>(T actor, string id, string address = null, IContext parent = null)
            where T : IActor;
    }
}