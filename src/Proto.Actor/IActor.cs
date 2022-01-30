// -----------------------------------------------------------------------
// <copyright file="IActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;

namespace Proto
{
    public interface IActor
    {
        Task ReceiveAsync(IContext context);
    }
}