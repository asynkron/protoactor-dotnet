using System;
// ReSharper disable once CheckNamespace
namespace Proto
{
    public interface ISupervisorStrategy
    {
        void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics rs, Exception cause, object? message);
    }
}