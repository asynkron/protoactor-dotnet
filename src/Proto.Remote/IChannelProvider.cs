// -----------------------------------------------------------------------
//   <copyright file="IChannelProvider.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Grpc.Core;

namespace Proto.Remote
{
    public interface IChannelProvider
    {
        ChannelBase GetChannel(string address);
    }
}