// -----------------------------------------------------------------------
//  <copyright file="Supervision.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;

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

    public static class Supervision
    {
        public static ISupervisorStrategy DefaultStrategy { get; } =
            new OneForOneStrategy((who, reason) => SupervisorDirective.Restart,10, TimeSpan.FromSeconds(10));
    }

    public interface ISupervisorStrategy
    {
        void HandleFailure(ISupervisor supervisor, PID child, ChildRestartStats crs, Exception cause);
    }

    public delegate SupervisorDirective Decider(PID pid, Exception reason);

    public class OneForOneStrategy : ISupervisorStrategy
    {
        private readonly int _maxNrOfRetries;
        private readonly TimeSpan? _withinTimeSpan;
        private readonly Decider _decider;

        public OneForOneStrategy(Decider decider, int maxNrOfRetries, TimeSpan? withinTimeSpan)
        {
            _decider = decider;
            _maxNrOfRetries = maxNrOfRetries;
            _withinTimeSpan = withinTimeSpan;
        }

        public void HandleFailure(ISupervisor supervisor, PID child, ChildRestartStats crs, Exception reason)
        {
            var directive = _decider(child, reason);
            switch (directive)
            {
                case SupervisorDirective.Resume:
                    //resume the failing child
                    child.SendSystemMessage(ResumeMailbox.Instance);
                    break;
                case SupervisorDirective.Restart:
                    //restart the failing child
                    if (crs.RequestRestartPermission(_maxNrOfRetries, _withinTimeSpan))
                    {
                        child.SendSystemMessage(new Restart());
                    }
                    else
                    {
                        child.Stop();
                    }
                    break;
                case SupervisorDirective.Stop:
                    //stop the failing child
                    child.Stop();
                    break;
                case SupervisorDirective.Escalate:
                    supervisor.EscalateFailure(child, reason);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}