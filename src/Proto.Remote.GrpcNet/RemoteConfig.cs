// -----------------------------------------------------------------------
//   <copyright file="RemoteConfig.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Proto.Remote
{
    public record RemoteConfig : RemoteConfigBase
    {
        protected RemoteConfig(string host, int port) : base(host, port)
        {

        }
        public static RemoteConfig BinToAllInterfaces(string advertisedHost, int port = 0) =>
            new RemoteConfig(AllInterfaces, port).WithAdvertisedHost(advertisedHost);
        
        public static RemoteConfig BindToLocalhost(int port = 0) => new RemoteConfig(Localhost, port);
        
        public static RemoteConfig BindTo(string host, int port = 0) => new RemoteConfig(host, port);
        public bool UseHttps { get; init; }
        public GrpcChannelOptions ChannelOptions { get; init; } = new GrpcChannelOptions();
        public Action<ListenOptions>? ConfigureKestrel { get; init; }
    }
}