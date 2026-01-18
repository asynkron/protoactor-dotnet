using System.Collections.Generic;
using FluentAssertions;
using k8s.Models;
using Xunit;

namespace Proto.Cluster.Kubernetes.Tests;

public class KubernetesExtensionsTests
{
    [Fact]
    public void GetMemberStatus_WhenAnnotationsAreMissing_ReturnsNull()
    {
        var pod = CreateRunningPod(
            labels: new Dictionary<string, string>
            {
                [ProtoLabels.LabelPort] = "12000",
                [ProtoLabels.LabelMemberId] = "member-1"
            },
            annotations: null
        );

        var config = new KubernetesProviderConfig();

        pod.GetMemberStatus(config).Should().BeNull();
    }

    [Fact]
    public void GetMemberStatus_WhenPortLabelIsMissing_ReturnsNull()
    {
        var pod = CreateRunningPod(
            labels: new Dictionary<string, string>
            {
                [ProtoLabels.LabelMemberId] = "member-1"
            },
            annotations: new Dictionary<string, string>
            {
                [ProtoLabels.AnnotationKinds] = "kind-1"
            }
        );

        var config = new KubernetesProviderConfig();

        pod.GetMemberStatus(config).Should().BeNull();
    }

    [Fact]
    public void GetMemberStatus_WhenPodHasRequiredLabelsAndAnnotations_ReturnsMemberStatus()
    {
        var pod = CreateRunningPod(
            labels: new Dictionary<string, string>
            {
                [ProtoLabels.LabelPort] = "12000",
                [ProtoLabels.LabelMemberId] = "member-1"
            },
            annotations: new Dictionary<string, string>
            {
                [ProtoLabels.AnnotationKinds] = "kind-1;kind-2"
            }
        );

        var config = new KubernetesProviderConfig();

        var status = pod.GetMemberStatus(config);
        status.Should().NotBeNull();
        status!.IsRunning.Should().BeTrue();
        status.IsReady.Should().BeTrue();
        status.Member.Id.Should().Be("member-1");
        status.Member.Port.Should().Be(12000);
        status.Member.Kinds.Should().Contain(new[] { "kind-1", "kind-2" });
    }

    private static V1Pod CreateRunningPod(
        Dictionary<string, string> labels,
        Dictionary<string, string> annotations
    ) =>
        new()
        {
            Metadata = new V1ObjectMeta
            {
                Name = "pod-1",
                NamespaceProperty = "default",
                Labels = labels,
                Annotations = annotations
            },
            Status = new V1PodStatus
            {
                Phase = "Running",
                PodIP = "10.0.0.1",
                ContainerStatuses = new List<V1ContainerStatus>
                {
                    new()
                    {
                        Ready = true
                    }
                }
            }
        };
}
