// -----------------------------------------------------------------------
//  <copyright file="RouterAddRoutee.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Messages
{
    public class RouterAddRoutee : RouterManagementMessage
    {
        public PID PID { get; set; }
    }
}