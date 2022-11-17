using Proto.Mailbox;

namespace Proto;

public sealed class WrapperProcess : Process
{
    private readonly Process _innerProcess;

    public WrapperProcess(Process innerProcess) : base(innerProcess.System)
    {
        _innerProcess = innerProcess;
    }

    protected internal override void SendUserMessage(PID pid, object message) =>
        _innerProcess.SendUserMessage(pid, message);

    
    protected internal override void SendSystemMessage(PID pid, SystemMessage message) =>
        _innerProcess.SendSystemMessage(pid, message);

}