using System;
using System.Collections.Generic;
using System.Text;

namespace Proto
{
    public enum SupervisorDirective
    {
        Resume,
        Restart,
        Stop,
        Escalate
    }

    public interface ISupervisor
    {
        PID[] Children();
        void EscalateFailure(PID who, Exception reason);
    }

    public interface ISupervisorStrategy
    {
        void HandleFailure(ISupervisor supervisor, PID child, Exception cause);
    }

    public delegate SupervisorDirective Decider(PID pid, Exception reason);

    public class OneForOneStrategy : ISupervisorStrategy
    {
        private Decider _decider;

        public OneForOneStrategy(Decider decider)
        {
            _decider = decider;
        }
        public void HandleFailure(ISupervisor supervisor, PID child, Exception reason)
        {
            var directive = _decider(child, reason);
            switch (directive) {
                case SupervisorDirective.Resume:
                    //resume the failing child
                    child.SendSystemMessage(new ResumeMailbox());
                    break;
	            case SupervisorDirective.Restart:
                    //restart the failing child
                    child.SendSystemMessage(new Restart());
                    break;
                case SupervisorDirective.Stop:
                    //stop the failing child
                    child.Stop();
                    break;
                case SupervisorDirective.Escalate:
                    supervisor.EscalateFailure(child, reason);
                    break;
            }
        }
    }
}
