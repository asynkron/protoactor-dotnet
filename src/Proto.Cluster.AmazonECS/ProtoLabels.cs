// -----------------------------------------------------------------------
// <copyright file="ProtoLabels.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Proto.Cluster.AmazonECS;

public static class ProtoLabels
{
    private const string LabelPrefix = "cluster.proto.actor/";
    public const string LabelPort = LabelPrefix + "port";
    public const string LabelHost = LabelPrefix + "host";
    public const string LabelKind = LabelPrefix + "kind";
    public const string LabelCluster = LabelPrefix + "cluster";
    public const string LabelStatusValue = LabelPrefix + "status-value";
    public const string LabelMemberId = LabelPrefix + "member-id";

    public static readonly string[] AllLabels =
        new[] {
            LabelPort,
            LabelHost,
            LabelCluster,
            LabelMemberId,
            LabelStatusValue
        };
}
