using System;

namespace Proto.TestFixtures
{
    public class DoNothingSupervisorStrategy : ISupervisorStrategy
    {
        public void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics crs, Exception cause) { }
    }
}
