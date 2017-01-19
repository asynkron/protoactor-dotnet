using System.Collections.Generic;

namespace Proto.Routing
{
    public class Routees
    {
        public Routees(List<PID> pids)
        {
            PIDs = pids;
        }

        public List<PID> PIDs { get; }
    }
}