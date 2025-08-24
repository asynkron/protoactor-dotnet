// -----------------------------------------------------------------------
// <copyright file="AwsHttpClient.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Proto.Cluster.AmazonECS;

[PublicAPI]
public class AwsEcsContainerMetadataHttpClient
{
    private readonly ILogger _logger = Log.CreateLogger<AwsEcsContainerMetadataHttpClient>();

    public ContainerMetadata GetContainerMetadata()
    {
        var str = Environment.GetEnvironmentVariable("ECS_CONTAINER_METADATA_URI_V4");

        try
        {
            if (Uri.TryCreate(str, UriKind.Absolute, out var containerMetadataUri))
            {
                var json = GetResponseString(containerMetadataUri);

                _logger.LogInformation("[AwsEcsContainerMetadataHttpClient] got metadata for container {Metadata}",
                    json);

                return JsonConvert.DeserializeObject<ContainerMetadata>(json);
            }

            _logger.LogError("[AwsEcsContainerMetadataHttpClient] failed to get Metadata {Url}", str);
        }
        catch (Exception x)
        {
            _logger.LogError(x, "[AwsEcsContainerMetadataHttpClient] failed to get Metadata {Url}", str);
        }

        return null;
    }

    public TaskMetadata GetTaskMetadata()
    {
        var str = Environment.GetEnvironmentVariable("ECS_CONTAINER_METADATA_URI_V4") + "/task";

        try
        {
            if (Uri.TryCreate(str, UriKind.Absolute, out var containerMetadataUri))
            {
                var json = GetResponseString(containerMetadataUri);
                _logger.LogInformation("[AwsEcsContainerMetadataHttpClient] got metadata for task {Metadata}", json);

                return JsonConvert.DeserializeObject<TaskMetadata>(json);
            }

            _logger.LogError("[AwsEcsContainerMetadataHttpClient] failed to get Metadata {Url}", str);
        }
        catch (Exception x)
        {
            _logger.LogError(x, "[AwsEcsContainerMetadataHttpClient] failed to get Metadata {Url}", str);
        }

        return null;
    }

    //
    // public string GetHostPrivateIPv4Address() => GetResponseString(new Uri("http://169.254.169.254/latest/meta-data/local-ipv4"));
    //
    // public string GetHostPublicIPv4Address() => GetResponseString(new Uri("http://169.254.169.254/latest/meta-data/public-ipv4"));

    private string GetResponseString(Uri requestUri)
    {
        try
        {
            var request = WebRequest.Create(requestUri);

            using var response = (HttpWebResponse)request.GetResponse();

            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogError("Failed to execute HTTP request. Request URI: {RequestUri}, Status code: {StatusCode}",
                    requestUri, response.StatusCode);

                return default;
            }

            using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream!);

            return reader.ReadToEnd();
        }
        catch (WebException ex) when (ex.Status == WebExceptionStatus.UnknownError)
        {
            _logger.LogError(ex, "Network is unreachable");
            // Network is unreachable
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get AWS metadata response");
        }

        return default;
    }
}

public class Limits
{
    [JsonProperty(nameof(CPU))] public int CPU { get; set; }
}

public class Network
{
    [JsonProperty(nameof(NetworkMode))] public string NetworkMode { get; set; }

    [JsonProperty(nameof(IPv4Addresses))] public List<string> IPv4Addresses { get; set; }

    [JsonProperty(nameof(AttachmentIndex))] public int AttachmentIndex { get; set; }

    [JsonProperty(nameof(MACAddress))] public string MACAddress { get; set; }

    [JsonProperty(nameof(IPv4SubnetCIDRBlock))] public string IPv4SubnetCIDRBlock { get; set; }

    [JsonProperty(nameof(DomainNameServers))] public List<string> DomainNameServers { get; set; }

    [JsonProperty(nameof(DomainNameSearchList))] public List<string> DomainNameSearchList { get; set; }

    [JsonProperty(nameof(PrivateDNSName))] public string PrivateDNSName { get; set; }

    [JsonProperty(nameof(SubnetGatewayIpv4Address))]
    public string SubnetGatewayIpv4Address { get; set; }
}

public class LogOptions
{
    [JsonProperty("awslogs-group")] public string AwslogsGroup { get; set; }

    [JsonProperty("awslogs-region")] public string AwslogsRegion { get; set; }

    [JsonProperty("awslogs-stream")] public string AwslogsStream { get; set; }
}

public class ContainerMetadata
{
    [JsonProperty(nameof(DockerId))] public string DockerId { get; set; }

    [JsonProperty(nameof(Name))] public string Name { get; set; }

    [JsonProperty(nameof(DockerName))] public string DockerName { get; set; }

    [JsonProperty(nameof(Image))] public string Image { get; set; }

    [JsonProperty(nameof(ImageID))] public string ImageID { get; set; }

    [JsonProperty(nameof(DesiredStatus))] public string DesiredStatus { get; set; }

    [JsonProperty(nameof(KnownStatus))] public string KnownStatus { get; set; }

    [JsonProperty(nameof(Limits))] public Limits Limits { get; set; }

    [JsonProperty(nameof(CreatedAt))] public string CreatedAt { get; set; }

    [JsonProperty(nameof(StartedAt))] public string StartedAt { get; set; }

    [JsonProperty(nameof(Type))] public string Type { get; set; }

    [JsonProperty(nameof(Networks))] public List<Network> Networks { get; set; }

    [JsonProperty(nameof(ContainerARN))] public string ContainerARN { get; set; }

    [JsonProperty(nameof(LogOptions))] public LogOptions LogOptions { get; set; }

    [JsonProperty(nameof(LogDriver))] public string LogDriver { get; set; }
}

public class Container
{
    [JsonProperty(nameof(DockerId))] public string DockerId { get; set; }

    [JsonProperty(nameof(Name))] public string Name { get; set; }

    [JsonProperty(nameof(DockerName))] public string DockerName { get; set; }

    [JsonProperty(nameof(Image))] public string Image { get; set; }

    [JsonProperty(nameof(ImageID))] public string ImageID { get; set; }

    [JsonProperty(nameof(DesiredStatus))] public string DesiredStatus { get; set; }

    [JsonProperty(nameof(KnownStatus))] public string KnownStatus { get; set; }

    [JsonProperty(nameof(Limits))] public Limits Limits { get; set; }

    [JsonProperty(nameof(CreatedAt))] public string CreatedAt { get; set; }

    [JsonProperty(nameof(StartedAt))] public string StartedAt { get; set; }

    [JsonProperty(nameof(Type))] public string Type { get; set; }

    [JsonProperty(nameof(Networks))] public List<Network> Networks { get; set; }

    [JsonProperty(nameof(LogDriver))] public string LogDriver { get; set; }

    [JsonProperty(nameof(LogOptions))] public LogOptions LogOptions { get; set; }

    [JsonProperty(nameof(ContainerARN))] public string ContainerARN { get; set; }
}

public class TaskMetadata
{
    [JsonProperty(nameof(Cluster))] public string Cluster { get; set; }

    [JsonProperty(nameof(TaskARN))] public string TaskARN { get; set; }

    [JsonProperty(nameof(Family))] public string Family { get; set; }

    [JsonProperty(nameof(Revision))] public string Revision { get; set; }

    [JsonProperty(nameof(DesiredStatus))] public string DesiredStatus { get; set; }

    [JsonProperty(nameof(KnownStatus))] public string KnownStatus { get; set; }

    [JsonProperty(nameof(PullStartedAt))] public string PullStartedAt { get; set; }

    [JsonProperty(nameof(PullStoppedAt))] public string PullStoppedAt { get; set; }

    [JsonProperty(nameof(AvailabilityZone))] public string AvailabilityZone { get; set; }

    [JsonProperty(nameof(LaunchType))] public string LaunchType { get; set; }

    [JsonProperty(nameof(Containers))] public List<Container> Containers { get; set; }
}