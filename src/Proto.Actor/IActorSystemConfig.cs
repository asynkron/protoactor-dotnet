// -----------------------------------------------------------------------
// <copyright file="IActorSystemOption.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;

namespace Proto
{
    public interface IActorSystemConfig
    {
        Task Apply(ActorSystem system);
    }
}