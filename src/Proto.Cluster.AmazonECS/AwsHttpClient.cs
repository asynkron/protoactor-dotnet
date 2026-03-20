// -----------------------------------------------------------------------
// <copyright file="AwsHttpClient.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.AmazonECS;

[PublicAPI]
public class AwsEcsContainerMetadataHttpClient
{
    private static readonly HttpClient HttpClient = new();
    private readonly ILogger _logger = Log.CreateLogger<AwsEcsContainerMetadataHttpClient>();

    public ContainerMetadata GetContainerMetadata()
    {
        var str = Environment.GetEnvironmentVariable("ECS_CONTAINER_METADATA_URI_V4");

        try
        {
            if (Uri.TryCreate(str, UriKind.Absolute, out var containerMetadataUri))
            {
                var json = GetResponseString(HttpMethod.Get, containerMetadataUri);

                _logger.LogInformation("[AwsEcsContainerMetadataHttpClient] got metadata for container {Metadata}",
                    json);

                return JsonSerializer.Deserialize<ContainerMetadata>(json);
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
                var json = GetResponseString(HttpMethod.Get, containerMetadataUri);
                _logger.LogInformation("[AwsEcsContainerMetadataHttpClient] got metadata for task {Metadata}", json);

                return JsonSerializer.Deserialize<TaskMetadata>(json);
            }

            _logger.LogError("[AwsEcsContainerMetadataHttpClient] failed to get Metadata {Url}", str);
        }
        catch (Exception x)
        {
            _logger.LogError(x, "[AwsEcsContainerMetadataHttpClient] failed to get Metadata {Url}", str);
        }

        return null;
    }

    public string GetHostPrivateIPv4Address()
    {
        try
        {
            var token = GetResponseString(HttpMethod.Put, new Uri("http://169.254.169.254/latest/api/token"), new Dictionary<string, string> { { "X-aws-ec2-metadata-token-ttl-seconds", "60" } });
            return GetResponseString(HttpMethod.Get, new Uri("http://169.254.169.254/latest/meta-data/local-ipv4"), new Dictionary<string, string> { { "X-aws-ec2-metadata-token", token } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AwsEcsContainerMetadataHttpClient] failed to get Host Private IPv4 Address");
        }

        return null;
    }

    public string GetHostPublicIPv4Address()
    {
        try
        {
            var token = GetResponseString(HttpMethod.Put, new Uri("http://169.254.169.254/latest/api/token"), new Dictionary<string, string> { { "X-aws-ec2-metadata-token-ttl-seconds", "60" } });
            return GetResponseString(HttpMethod.Get, new Uri("http://169.254.169.254/latest/meta-data/public-ipv4"), new Dictionary<string, string> { { "X-aws-ec2-metadata-token", token } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AwsEcsContainerMetadataHttpClient] failed to get Host Public IPv4 Address");
        }

        return null;
    }

    private string GetResponseString(HttpMethod method, Uri requestUri, Dictionary<String, String> headers = null)
    {
        try
        {
            var request = new HttpRequestMessage(method, requestUri);
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }
            using var response = HttpClient.SendAsync(request).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to execute HTTP request. Method: {Method}, Request URI: {RequestUri}, Status code: {StatusCode}",
                    method, requestUri, response.StatusCode);

                return default;
            }

            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get AWS metadata response");
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
    [JsonPropertyName("CPU")] public int CPU { get; set; }
}

public class Network
{
    [JsonPropertyName("NetworkMode")] public string NetworkMode { get; set; }

    [JsonPropertyName("IPv4Addresses")] public List<string> IPv4Addresses { get; set; }

    [JsonPropertyName("AttachmentIndex")] public int AttachmentIndex { get; set; }

    [JsonPropertyName("MACAddress")] public string MACAddress { get; set; }

    [JsonPropertyName("IPv4SubnetCIDRBlock")] public string IPv4SubnetCIDRBlock { get; set; }

    [JsonPropertyName("DomainNameServers")] public List<string> DomainNameServers { get; set; }

    [JsonPropertyName("DomainNameSearchList")] public List<string> DomainNameSearchList { get; set; }

    [JsonPropertyName("PrivateDNSName")] public string PrivateDNSName { get; set; }

    [JsonPropertyName("SubnetGatewayIpv4Address")]
    public string SubnetGatewayIpv4Address { get; set; }
}

public class LogOptions
{
    [JsonPropertyName("awslogs-group")] public string AwslogsGroup { get; set; }

    [JsonPropertyName("awslogs-region")] public string AwslogsRegion { get; set; }

    [JsonPropertyName("awslogs-stream")] public string AwslogsStream { get; set; }
}

public class ContainerMetadata
{
    [JsonPropertyName("DockerId")] public string DockerId { get; set; }

    [JsonPropertyName("Name")] public string Name { get; set; }

    [JsonPropertyName("DockerName")] public string DockerName { get; set; }

    [JsonPropertyName("Image")] public string Image { get; set; }

    [JsonPropertyName("ImageID")] public string ImageID { get; set; }

    [JsonPropertyName("DesiredStatus")] public string DesiredStatus { get; set; }

    [JsonPropertyName("KnownStatus")] public string KnownStatus { get; set; }

    [JsonPropertyName("Limits")] public Limits Limits { get; set; }

    [JsonPropertyName("CreatedAt")] public string CreatedAt { get; set; }

    [JsonPropertyName("StartedAt")] public string StartedAt { get; set; }

    [JsonPropertyName("Type")] public string Type { get; set; }

    [JsonPropertyName("Networks")] public List<Network> Networks { get; set; }

    [JsonPropertyName("ContainerARN")] public string ContainerARN { get; set; }

    [JsonPropertyName("LogOptions")] public LogOptions LogOptions { get; set; }

    [JsonPropertyName("LogDriver")] public string LogDriver { get; set; }
}

public class PortMapping
{
    [JsonPropertyName("ContainerPort")] public int ContainerPort { get; set; }

    [JsonPropertyName("Protocol")] public string Protocol { get; set; }

    [JsonPropertyName("HostPort")] public int HostPort { get; set; }

    [JsonPropertyName("HostIp")] public string HostIP { get; set; }
}

public class Container
{
    [JsonPropertyName("DockerId")] public string DockerId { get; set; }

    [JsonPropertyName("Name")] public string Name { get; set; }

    [JsonPropertyName("DockerName")] public string DockerName { get; set; }

    [JsonPropertyName("Image")] public string Image { get; set; }

    [JsonPropertyName("ImageID")] public string ImageID { get; set; }

    [JsonPropertyName("Ports")] public List<PortMapping> Ports { get; set; }

    [JsonPropertyName("DesiredStatus")] public string DesiredStatus { get; set; }

    [JsonPropertyName("KnownStatus")] public string KnownStatus { get; set; }

    [JsonPropertyName("Limits")] public Limits Limits { get; set; }

    [JsonPropertyName("CreatedAt")] public string CreatedAt { get; set; }

    [JsonPropertyName("StartedAt")] public string StartedAt { get; set; }

    [JsonPropertyName("Type")] public string Type { get; set; }

    [JsonPropertyName("Networks")] public List<Network> Networks { get; set; }

    [JsonPropertyName("LogDriver")] public string LogDriver { get; set; }

    [JsonPropertyName("LogOptions")] public LogOptions LogOptions { get; set; }

    [JsonPropertyName("ContainerARN")] public string ContainerARN { get; set; }
}

public class TaskMetadata
{
    [JsonPropertyName("Cluster")] public string Cluster { get; set; }

    [JsonPropertyName("TaskARN")] public string TaskARN { get; set; }

    [JsonPropertyName("Family")] public string Family { get; set; }

    [JsonPropertyName("Revision")] public string Revision { get; set; }

    [JsonPropertyName("DesiredStatus")] public string DesiredStatus { get; set; }

    [JsonPropertyName("KnownStatus")] public string KnownStatus { get; set; }

    [JsonPropertyName("PullStartedAt")] public string PullStartedAt { get; set; }

    [JsonPropertyName("PullStoppedAt")] public string PullStoppedAt { get; set; }

    [JsonPropertyName("AvailabilityZone")] public string AvailabilityZone { get; set; }

    [JsonPropertyName("LaunchType")] public string LaunchType { get; set; }

    [JsonPropertyName("Containers")] public List<Container> Containers { get; set; }
}
