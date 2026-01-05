using System.Threading.Tasks;
using Proto.Diagnostics;

namespace Proto;

public class NamedActor : IActor, IDiagnosticsTypeName
{
    private readonly IActor _actor;
    private readonly string _name;

    public NamedActor(IActor actor, string name)
    {
        _actor = actor;
        _name = name;
    }

    public Task ReceiveAsync(IContext context)
    {
        return _actor.ReceiveAsync(context);
    }

    public string GetTypeName()
    {
        return _name;
    }
}
