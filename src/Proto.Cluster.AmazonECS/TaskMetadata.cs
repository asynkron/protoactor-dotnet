// -----------------------------------------------------------------------
// <copyright file="TaskMetadata.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Proto.Cluster.AmazonECS
{
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