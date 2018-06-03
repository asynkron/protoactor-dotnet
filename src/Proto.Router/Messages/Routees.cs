// -----------------------------------------------------------------------
//   <copyright file="Routees.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
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