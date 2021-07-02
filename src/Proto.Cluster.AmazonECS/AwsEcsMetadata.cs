// -----------------------------------------------------------------------
// <copyright file="AwsEcsMetadata.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Proto.Cluster.AmazonECS
{
    public class PortMapping
    {
        [JsonProperty("ContainerPort")]
        public int ContainerPort { get; set; }

        [JsonProperty("HostPort")]
        public int HostPort { get; set; }

        [JsonProperty("BindIp")]
        public string BindIp { get; set; }

        [JsonProperty("Protocol")]
        public string Protocol { get; set; }
    }

    public class Network
    {
        [JsonProperty("NetworkMode")]
        public string NetworkMode { get; set; }

        [JsonProperty("IPv4Addresses")]
        public List<string> IPv4Addresses { get; set; }
    }

    public class Metadata
    {
        [JsonProperty("Cluster")]
        public string Cluster { get; set; }

        [JsonProperty("ContainerInstanceARN")]
        public string ContainerInstanceARN { get; set; }

        [JsonProperty("TaskARN")]
        public string TaskARN { get; set; }

        [JsonProperty("ContainerID")]
        public string ContainerID { get; set; }

        [JsonProperty("ContainerName")]
        public string ContainerName { get; set; }

        [JsonProperty("DockerContainerName")]
        public string DockerContainerName { get; set; }

        [JsonProperty("ImageID")]
        public string ImageID { get; set; }

        [JsonProperty("ImageName")]
        public string ImageName { get; set; }

        [JsonProperty("PortMappings")]
        public List<PortMapping> PortMappings { get; set; }

        [JsonProperty("Networks")]
        public List<Network> Networks { get; set; }

        [JsonProperty("MetadataFileStatus")]
        public string MetadataFileStatus { get; set; }
    }
}