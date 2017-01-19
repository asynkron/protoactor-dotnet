using System.Collections.Generic;

namespace Proto.Router.Messages
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