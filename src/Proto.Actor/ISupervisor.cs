using System;
using System.Collections.Immutable;

namespace Proto
{
    public interface ISupervisor
    {
        IImmutableSet<PID> Children { get; }
        void EscalateFailure(Exception reason, object? message);
        void RestartChildren(Exception reason, params PID[] pids);
        void StopChildren(params PID[] pids);
        void ResumeChildren(params PID[] pids);
    }
}