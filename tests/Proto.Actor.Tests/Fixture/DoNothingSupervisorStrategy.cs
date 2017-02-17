using System;
using System.Collections.Generic;
using System.Text;

namespace Proto.Tests.Fixture
{
    public class DoNothingSupervisorStrategy : ISupervisorStrategy
    {
        public void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics crs, Exception cause) { }
    }
}
