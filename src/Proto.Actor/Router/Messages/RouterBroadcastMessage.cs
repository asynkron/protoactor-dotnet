// -----------------------------------------------------------------------
//   <copyright file="RouterBroadcastMessage.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Messages
{
    public class RouterBroadcastMessage : RouterManagementMessage
    {
        public RouterBroadcastMessage(object message)
        {
            Message = message;
        }

        public object Message { get; }
    }
}