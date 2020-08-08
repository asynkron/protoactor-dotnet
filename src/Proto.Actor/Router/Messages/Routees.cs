// -----------------------------------------------------------------------
//   <copyright file="Routees.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

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