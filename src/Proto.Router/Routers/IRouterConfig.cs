// -----------------------------------------------------------------------
//  <copyright file="IRouterConfig.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Routers
{
    public interface IRouterConfig
    {
        void OnStarted(IContext context, Props props, RouterState router);

        RouterState CreateRouterState();
    }
}