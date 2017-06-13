// -----------------------------------------------------------------------
//  <copyright file="IRouterConfig.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

namespace Proto.Router.Routers
{
    public interface IRouterConfig
    {
        Task OnStartedAsync(IContext context, Props props, RouterState router);

        RouterState CreateRouterState();
    }
}