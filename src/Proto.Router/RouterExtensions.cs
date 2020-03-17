// -----------------------------------------------------------------------
//   <copyright file="Router.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router
{
    public static class RouterExtensions
    {
        public static Router GetRouters(this ISenderContext senderContext)
        {
            return new Router(senderContext);
        }
        public static Router GetRouters(this ActorSystem actorSystem)
        {
            return actorSystem.Root.GetRouters();
        }
    }
}