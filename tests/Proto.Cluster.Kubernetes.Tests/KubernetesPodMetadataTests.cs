using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Proto.Cluster.Kubernetes.Tests;

public class KubernetesPodMetadataTests
{
    [Fact]
    public void ToNonClusterDictionary_WhenSourceIsNull_ReturnsEmptyDictionary()
    {
        KubernetesPodMetadata.ToNonClusterDictionary(null).Should().BeEmpty();
    }

    [Fact]
    public void ToNonClusterDictionary_StripsProtoClusterEntries()
    {
        var source = new Dictionary<string, string>
        {
            [ProtoLabels.LabelHost] = "stale-host",
            ["app"] = "proto"
        };

        var result = KubernetesPodMetadata.ToNonClusterDictionary(source);

        result.Should().NotContainKey(ProtoLabels.LabelHost);
        result.Should().ContainKey("app").WhoseValue.Should().Be("proto");
    }

    [Fact]
    public void TryAddNonClusterEntries_DoesNotAddProtoClusterEntries()
    {
        var source = new Dictionary<string, string>
        {
            [ProtoLabels.LabelHost] = "stale-host",
            ["app"] = "proto"
        };

        var destination = new Dictionary<string, string>();

        KubernetesPodMetadata.TryAddNonClusterEntries(source, destination);

        destination.Should().NotContainKey(ProtoLabels.LabelHost);
        destination.Should().ContainKey("app").WhoseValue.Should().Be("proto");
    }
}

