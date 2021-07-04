// -----------------------------------------------------------------------
// <copyright file="EcsUtils.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.ECS;
using Amazon.ECS.Model;
using Microsoft.Extensions.Logging;
using Task = Amazon.ECS.Model.Task;

namespace Proto.Cluster.AmazonECS
{
    public static class EcsUtils
    {
        private static readonly ILogger Logger = Log.CreateLogger(nameof(EcsUtils));
        public static async Task<Member[]> GetMembers(this AmazonECSClient c, string ecsClusterName)
        {
            var allTasks = await c.ListTasksAsync(new ListTasksRequest()
                {
                    Cluster = ecsClusterName
                }
            );
            
            var instanceArns = allTasks.TaskArns;

            if (!instanceArns.Any())
            {
                return Array.Empty<Member>();
            }

            var describedTasks = await c.DescribeTasksAsync(new DescribeTasksRequest
                {
                    Include = {"TAGS"},
                    Tasks = instanceArns,
                }
            );
            
            var members = new List<Member>();
            foreach (var task in describedTasks.Tasks)
            {
                var metadata = task.GetMetadata();
                
                if (!metadata.ContainsKey(ProtoLabels.LabelMemberId))
                {
                    Logger.LogWarning("Skipping Task {Arn}, no Proto Tags found", task.TaskArn);
                    continue;
                }
                
                var kinds = metadata
                    .Where(kvp => kvp.Key.StartsWith(ProtoLabels.LabelKind))
                    .Select(kvp => kvp.Key[(ProtoLabels.LabelKind.Length+1)..]).ToArray();
                
                var member = new Member
                {
                    Id = metadata[ProtoLabels.LabelMemberId],
                    Port = int.Parse(metadata[ProtoLabels.LabelPort]),
                    Host = task.Containers.First().NetworkInterfaces.First().PrivateIpv4Address,
                    Kinds = { kinds }
                };
                
                members.Add(member);
            }

            return members.ToArray();
        }

        public static IDictionary<string, string> GetMetadata(this Task task) => task.Tags.ToDictionary(t => t.Key, t => t.Value);

        public static async System.Threading.Tasks.Task UpdateMetadata(this AmazonECSClient c, string resourceArn, IDictionary<string, string> metadata)
        {
            var tags = metadata.Select(kvp => new Tag
                {
                    Key = kvp.Key,
                    Value = kvp.Value,
                }
            ).ToList();
            
            await c.TagResourceAsync(new TagResourceRequest
            {
                ResourceArn = resourceArn,
                Tags = tags,
            });
        }
    }
}