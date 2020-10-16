using System;
// ReSharper disable once CheckNamespace
namespace Proto
{
    public class AlwaysRestartStrategy : ISupervisorStrategy
    {
        //always restart
        public void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics rs, Exception reason,
            object? message)
            => supervisor.RestartChildren(reason, child);
    }
}