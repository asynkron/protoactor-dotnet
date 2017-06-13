using System;
using System.Threading.Tasks;

namespace Proto.TestFixtures
{
    public class DoNothingSupervisorStrategy : ISupervisorStrategy
    {
        public Task HandleFailureAsync(ISupervisor supervisor, PID child, RestartStatistics rs, Exception cause)
        {
            return Actor.Done;
        }
    }
}
