// -----------------------------------------------------------------------
// <copyright file="AwsHttpClient.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Proto;
using Proto.Cluster.AmazonECS;

namespace EcsDiagnostics
{
    public class AwsMetaClient
    {
        private readonly ILogger _logger = Log.CreateLogger<AwsEcsContainerMetadataHttpClient>();
        
        
        //
        // public string GetFoo()
        // {
        //     var str = Environment.GetEnvironmentVariable("ECS_CONTAINER_METADATA_URI_V4") + "/meta-data/local-ipv";
        //     try
        //     {
        //         if (Uri.TryCreate(str, UriKind.Absolute, out var containerMetadataUri))
        //         {
        //             var json = GetResponseString(containerMetadataUri);
        //             _logger.LogInformation("[AwsEcsContainerMetadataHttpClient] got metadata for container {Metadata}", json);
        //             return json;
        //         }
        //
        //         _logger.LogError("[AwsEcsContainerMetadataHttpClient] failed to get Metadata {Url}",str);
        //
        //     }
        //     catch(Exception x)
        //     {
        //         _logger.LogError(x, "[AwsEcsContainerMetadataHttpClient] failed to get Metadata {Url}",str);
        //     }
        //
        //     return null;
        // }

        public ContainerMetadata GetContainerMetadata()
        {
            var str = Environment.GetEnvironmentVariable("ECS_CONTAINER_METADATA_URI_V4");
            try
            {
                if (Uri.TryCreate(str, UriKind.Absolute, out var containerMetadataUri))
                {
                    var json = GetResponseString(containerMetadataUri);
                    _logger.LogInformation("[AwsEcsContainerMetadataHttpClient] got metadata for container {Metadata}", json);
                    return JsonConvert.DeserializeObject<ContainerMetadata>(json);
                }

                _logger.LogError("[AwsEcsContainerMetadataHttpClient] failed to get Metadata {Url}",str);

            }
            catch(Exception x)
            {
                _logger.LogError(x, "[AwsEcsContainerMetadataHttpClient] failed to get Metadata {Url}",str);
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
            catch(Exception x)
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
                    _logger.LogError("Failed to execute HTTP request. Request URI: {RequestUri}, Status code: {StatusCode}", requestUri, response.StatusCode);

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
        [JsonProperty("CPU")]
        public int CPU { get; set; }
    }

    public class Network
    {
        [JsonProperty("NetworkMode")]
        public string NetworkMode { get; set; }

        [JsonProperty("IPv4Addresses")]
        public List<string> IPv4Addresses { get; set; }

        [JsonProperty("AttachmentIndex")]
        public int AttachmentIndex { get; set; }

        [JsonProperty("MACAddress")]
        public string MACAddress { get; set; }

        [JsonProperty("IPv4SubnetCIDRBlock")]
        public string IPv4SubnetCIDRBlock { get; set; }

        [JsonProperty("DomainNameServers")]
        public List<string> DomainNameServers { get; set; }

        [JsonProperty("DomainNameSearchList")]
        public List<string> DomainNameSearchList { get; set; }

        [JsonProperty("PrivateDNSName")]
        public string PrivateDNSName { get; set; }

        [JsonProperty("SubnetGatewayIpv4Address")]
        public string SubnetGatewayIpv4Address { get; set; }
    }

    public class LogOptions
    {
        [JsonProperty("awslogs-group")]
        public string AwslogsGroup { get; set; }

        [JsonProperty("awslogs-region")]
        public string AwslogsRegion { get; set; }

        [JsonProperty("awslogs-stream")]
        public string AwslogsStream { get; set; }
    }

    public class ContainerMetadata
    {
        [JsonProperty("DockerId")]
        public string DockerId { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("DockerName")]
        public string DockerName { get; set; }

        [JsonProperty("Image")]
        public string Image { get; set; }

        [JsonProperty("ImageID")]
        public string ImageID { get; set; }

        [JsonProperty("DesiredStatus")]
        public string DesiredStatus { get; set; }

        [JsonProperty("KnownStatus")]
        public string KnownStatus { get; set; }

        [JsonProperty("Limits")]
        public Limits Limits { get; set; }

        [JsonProperty("CreatedAt")]
        public string CreatedAt { get; set; }

        [JsonProperty("StartedAt")]
        public string StartedAt { get; set; }

        [JsonProperty("Type")]
        public string Type { get; set; }

        [JsonProperty("Networks")]
        public List<Network> Networks { get; set; }

        [JsonProperty("ContainerARN")]
        public string ContainerARN { get; set; }

        [JsonProperty("LogOptions")]
        public LogOptions LogOptions { get; set; }

        [JsonProperty("LogDriver")]
        public string LogDriver { get; set; }
    }
    
    public class Container
    {
        [JsonProperty("DockerId")]
        public string DockerId { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("DockerName")]
        public string DockerName { get; set; }

        [JsonProperty("Image")]
        public string Image { get; set; }

        [JsonProperty("ImageID")]
        public string ImageID { get; set; }

        [JsonProperty("DesiredStatus")]
        public string DesiredStatus { get; set; }

        [JsonProperty("KnownStatus")]
        public string KnownStatus { get; set; }

        [JsonProperty("Limits")]
        public Limits Limits { get; set; }

        [JsonProperty("CreatedAt")]
        public string CreatedAt { get; set; }

        [JsonProperty("StartedAt")]
        public string StartedAt { get; set; }

        [JsonProperty("Type")]
        public string Type { get; set; }

        [JsonProperty("Networks")]
        public List<Network> Networks { get; set; }

        [JsonProperty("LogDriver")]
        public string LogDriver { get; set; }

        [JsonProperty("LogOptions")]
        public LogOptions LogOptions { get; set; }

        [JsonProperty("ContainerARN")]
        public string ContainerARN { get; set; }
    }

    public class TaskMetadata
    {
        [JsonProperty("Cluster")]
        public string Cluster { get; set; }

        [JsonProperty("TaskARN")]
        public string TaskARN { get; set; }

        [JsonProperty("Family")]
        public string Family { get; set; }

        [JsonProperty("Revision")]
        public string Revision { get; set; }

        [JsonProperty("DesiredStatus")]
        public string DesiredStatus { get; set; }

        [JsonProperty("KnownStatus")]
        public string KnownStatus { get; set; }

        [JsonProperty("PullStartedAt")]
        public string PullStartedAt { get; set; }

        [JsonProperty("PullStoppedAt")]
        public string PullStoppedAt { get; set; }

        [JsonProperty("AvailabilityZone")]
        public string AvailabilityZone { get; set; }

        [JsonProperty("LaunchType")]
        public string LaunchType { get; set; }

        [JsonProperty("Containers")]
        public List<Container> Containers { get; set; }
    }
}