// -----------------------------------------------------------------------
//   <copyright file="RouterRemoveRoutee.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Messages
{
    public class RouterRemoveRoutee : RouterManagementMessage
    {
        public RouterRemoveRoutee(PID pid)
        {
            PID = pid;
        }

        public PID PID { get; }
    }
}