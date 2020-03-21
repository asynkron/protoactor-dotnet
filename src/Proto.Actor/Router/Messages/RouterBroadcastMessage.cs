// -----------------------------------------------------------------------
//   <copyright file="RouterBroadcastMessage.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Messages
{
    public class RouterBroadcastMessage : RouterManagementMessage
    {
        public RouterBroadcastMessage(object message) => Message = message;

        public object Message { get; }
    }
}
