// -----------------------------------------------------------------------
//   <copyright file="RouterRemoveRoutee.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------
namespace Proto.Router.Messages
{
    public class RouterRemoveRoutee : RouterManagementMessage
    {
        public PID PID { get; set; }
    }
}