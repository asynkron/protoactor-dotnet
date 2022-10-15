// -----------------------------------------------------------------------
//   <copyright file="GrpcNetRemoteConfig.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Grpc.Net.Client;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Proto.Remote.GrpcNet;

[PublicAPI]
public record GrpcNetRemoteConfig : RemoteConfigBase
{
    protected GrpcNetRemoteConfig(string host, int port) : base(host, port)
    {
    }

    /// <summary>
    ///     Set to true to enable SSL on the channel
    /// </summary>
    public bool UseHttps { get; init; }

    /// <summary>
    ///     Channel options for the gRPC channel
    /// </summary>
    [JsonIgnore]
    public GrpcChannelOptions ChannelOptions { get; init; } = new();

    /// <summary>
    ///     A delegate that performs additional configuration on Kestrel <see cref="ListenOptions" />.
    ///     If not supplied, the default implementation sets the protocol to HTTP2
    /// </summary>
    [JsonIgnore]
    public Action<ListenOptions>? ConfigureKestrel { get; init; }

    /// <summary>
    ///     A delegate that allows to choose the address for the <see cref="ActorSystem" /> from the list of addresses Kestrel
    ///     listens on.
    ///     By default, the first address is used.
    /// </summary>
    [JsonIgnore]
    public Func<IEnumerable<Uri>?, Uri?> UriChooser { get; init; } = uris => uris?.FirstOrDefault();

    /// <summary>
    ///     Creates new <see cref="GrpcNetRemoteConfig" /> instance that binds to all network interfaces
    /// </summary>
    /// <param name="advertisedHost">
    ///     The advertised hostname for the remote system.
    ///     If the remote system is behind e.g. a NAT or reverse proxy, this needs to be set to
    ///     the external hostname in order for other systems to be able to connect to it.
    /// </param>
    /// <param name="port">Port to bind on, 0 (default) means random port</param>
    /// <returns></returns>
    public static GrpcNetRemoteConfig BindToAllInterfaces(string? advertisedHost = null, int port = 0) =>
        new GrpcNetRemoteConfig(AllInterfaces, port).WithAdvertisedHost(advertisedHost);

    /// <summary>
    ///     Creates new <see cref="GrpcNetRemoteConfig" /> instance that binds to a loopback interface. Useful for local
    ///     development.
    /// </summary>
    /// <param name="port">Port to bind on, 0 (default) means random port</param>
    /// <returns></returns>
    public static GrpcNetRemoteConfig BindToLocalhost(int port = 0) => new(Localhost, port);

    /// <summary>
    ///     Creates new <see cref="GrpcNetRemoteConfig" /> instance that binds to a specific network interface.
    /// </summary>
    /// <param name="host">Host to bind to</param>
    /// <param name="port">Port to bind on, 0 (default) means random port</param>
    /// <returns></returns>
    public static GrpcNetRemoteConfig BindTo(string host, int port = 0) => new(host, port);
}