// -----------------------------------------------------------------------
//  <copyright file="Supervision.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Proto.Mailbox;

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
        IReadOnlyCollection<PID> Children { get; }
        void EscalateFailure(PID who, Exception reason);
    }

    public static class Supervision
    {
        public static ISupervisorStrategy DefaultStrategy { get; } =
            new OneForOneStrategy((who, reason) => SupervisorDirective.Restart,10, TimeSpan.FromSeconds(10));
    }

    public interface ISupervisorStrategy
    {
        void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics crs, Exception cause);
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

        //public bool RequestRestartPermission(int maxNrOfRetries, TimeSpan? withinTimeSpan)
        //{
        //    if (maxNrOfRetries == 0)
        //    {
        //        return false;
        //    }

        //    FailureCount++;

        //    //supervisor says child may restart, and we don't care about any timewindow
        //    if (withinTimeSpan == null)
        //    {
        //        return FailureCount <= maxNrOfRetries;
        //    }

        //    var max = DateTime.Now - withinTimeSpan;
        //    if (LastFailureTime > max)
        //    {
        //        return FailureCount <= maxNrOfRetries;
        //    }

        //    //we are past the time limit, we can safely reset the failure count and restart
        //    FailureCount = 0;
        //    return true;
        //}

        public void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics crs, Exception reason)
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
                        Console.WriteLine($"Restarting {child.ToShortString()} Reason {reason}");
                        child.SendSystemMessage(Restart.Instance);
                    }
                    else
                    {
                        Console.WriteLine($"Stopping {child.ToShortString()} Reason { reason}");
                        child.Stop();
                    }
                    break;
                case SupervisorDirective.Stop:
                    //stop the failing child
                    Console.WriteLine($"Stopping {child.ToShortString()} Reason {reason}");
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