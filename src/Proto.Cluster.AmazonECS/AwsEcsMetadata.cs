// -----------------------------------------------------------------------
// <copyright file="AwsEcsMetadata.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Proto.Cluster.AmazonECS
{
  

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

    public class Metadata
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


}