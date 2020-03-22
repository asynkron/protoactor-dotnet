// -----------------------------------------------------------------------
//   <copyright file="RouterAddRoutee.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Messages
{
    public class RouterAddRoutee : RouterManagementMessage
    {
        public RouterAddRoutee(PID pid) => PID = pid;
        public PID PID { get; }
    }
}
