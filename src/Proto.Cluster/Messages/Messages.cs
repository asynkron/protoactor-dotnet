// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Google.Protobuf;
using Proto.Remote;

// ReSharper disable once CheckNamespace
namespace Proto.Cluster;

public sealed partial class ClusterIdentity : ICustomDiagnosticMessage
{
    internal PID? CachedPid { get; set; }

    public string ToDiagnosticString() => $"{Kind}/{Identity}";

    /// <summary>
    ///     Creates ClusterIdentity from identity and cluster kind
    /// </summary>
    /// <param name="identity"></param>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static ClusterIdentity Create(string identity, string kind) =>
        new()
        {
            Identity = identity,
            Kind = kind
        };
}

public sealed partial class ActivationRequest
{
    public string Kind => ClusterIdentity.Kind;
    public string Identity => ClusterIdentity.Identity;
}

public sealed partial class ActivationTerminated
{
    public string Kind => ClusterIdentity.Kind;
    public string Identity => ClusterIdentity.Identity;
}

public sealed partial class Activation
{
    public string Kind => ClusterIdentity.Kind;
    public string Identity => ClusterIdentity.Identity;
}

public sealed partial class IdentityHandover : IRootSerializable
{
    public IRootSerialized Serialize(ActorSystem system) =>
        new RemoteIdentityHandover
        {
            Actors = PackedActivations.Pack(system.Address, Actors),
            TopologyHash = TopologyHash,
            Final = Final,
            Skipped = Skipped,
            ChunkId = ChunkId,
            Sent = Sent
        };
}

public sealed partial class RemoteIdentityHandover : IRootSerialized
{
    public IRootSerializable Deserialize(ActorSystem system) =>
        new IdentityHandover
        {
            TopologyHash = TopologyHash,
            Final = Final,
            Skipped = Skipped,
            Sent = Sent,
            ChunkId = ChunkId,
            Actors = { Actors.UnPack() }
        };
}

public sealed partial class PackedActivations
{
    public IEnumerable<Activation> UnPack() => Actors.SelectMany(UnpackKind);

    private IEnumerable<Activation> UnpackKind(Types.Kind kind) =>
        kind.Activations.Select(packed => new Activation
            {
                ClusterIdentity = ClusterIdentity.Create(packed.Identity, kind.Name),
                Pid = PID.FromAddress(Address, packed.ActivationId)
            }
        );

    public static PackedActivations Pack(string address, IEnumerable<Activation> activations) =>
        new()
        {
            Address = address,
            Actors = { PackActivations(activations) }
        };

    private static IEnumerable<Types.Kind> PackActivations(IEnumerable<Activation> activations) =>
        activations.GroupBy(it => it.Kind)
            .Select(grouping => new Types.Kind
                {
                    Name = grouping.Key,
                    Activations =
                    {
                        grouping.Select(activation => new Types.Activation
                            {
                                Identity = activation.Identity,
                                ActivationId = activation.Pid.Id
                            }
                        )
                    }
                }
            );
}

public partial class ClusterTopology
{
    /// <summary>
    ///     Topology based logic (IE partition based) can use this token to cancel any work when this topology is no longer
    ///     valid
    /// </summary>
    public CancellationToken? TopologyValidityToken { get; init; }

    //this ignores joined and left members, only the actual members are relevant
    public uint GetMembershipHashCode() => Member.TopologyHash(Members);
}

public partial class Member
{
    public static uint TopologyHash(IEnumerable<Member> members)
    {
        var x = members.Select(m => m.Id).OrderBy(i => i).ToArray();
        var key = string.Concat(x);
        var hash = MurmurHash2.Hash(key);

        return hash;
    }
}