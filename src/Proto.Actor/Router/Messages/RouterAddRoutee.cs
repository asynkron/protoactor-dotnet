// -----------------------------------------------------------------------
//   <copyright file="RouterAddRoutee.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Messages
{
    public class RouterAddRoutee : RouterManagementMessage
    {
        public RouterAddRoutee(PID pid)
        {
            PID = pid;
        }

        public PID PID { get; }
    }
}