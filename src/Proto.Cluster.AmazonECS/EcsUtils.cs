// -----------------------------------------------------------------------
// <copyright file="EcsUtils.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
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

namespace Proto.Cluster.AmazonECS;

public static class EcsUtils
{
    private static readonly ILogger Logger = Log.CreateLogger(nameof(EcsUtils));

    public static async Task<Member[]> GetMembers(this IAmazonECS c, string ecsClusterName)
    {
        var allTasks = await c.ListTasksAsync(new ListTasksRequest
        {
            Cluster = ecsClusterName
        }
        ).ConfigureAwait(false);

        var instanceArns = allTasks.TaskArns ?? new List<string>();

        if (instanceArns.Count == 0)
        {
            return Array.Empty<Member>();
        }

        var describedTasks = await c.DescribeTasksAsync(new DescribeTasksRequest
        {
            Include = new List<string> { "TAGS" },
            Cluster = ecsClusterName,
            Tasks = instanceArns
        }
        ).ConfigureAwait(false);

        var members = new List<Member>();

        foreach (var task in describedTasks.Tasks ?? new List<Task>())
        {
            if (task.DesiredStatus != DesiredStatus.RUNNING)
            {
                Logger.LogDebug("Skipping Task {Arn}, not in desired state", task.TaskArn);
                continue;
            }

            var metadata = task.GetMetadata();

            if (!metadata.ContainsKey(ProtoLabels.LabelMemberId))
            {
                Logger.LogDebug("Skipping Task {Arn}, no Proto Tags found", task.TaskArn);

                continue;
            }

            var kinds = metadata
                .Where(kvp => kvp.Key.StartsWith(ProtoLabels.LabelKind))
                .Select(kvp => kvp.Key[(ProtoLabels.LabelKind.Length + 1)..])
                .ToArray();
            var port = int.Parse(metadata[ProtoLabels.LabelPort]);
            metadata.TryGetValue(ProtoLabels.LabelHost, out string host);

            var container = task.Containers.First();
            var @interface = container.NetworkInterfaces?.FirstOrDefault();

            if (@interface == null && string.IsNullOrEmpty(host))
            {
                Logger.LogWarning("Skipping Task {Arn}, no network information found", task.TaskArn);
                continue;
            }

            var member = new Member
            {
                Id = metadata[ProtoLabels.LabelMemberId],
                Port = port,
                Host = host ?? @interface?.PrivateIpv4Address,
                Kinds = { kinds }
            };

            members.Add(member);
        }

        return members.ToArray();
    }

    public static IDictionary<string, string> GetMetadata(this Task task) =>
        (task.Tags ?? new List<Tag>()).ToDictionary(t => t.Key, t => t.Value);

    public static async System.Threading.Tasks.Task UpdateMetadata(this IAmazonECS c, string resourceArn,
        IDictionary<string, string> metadata)
    {
        var tags = metadata.Select(kvp => new Tag
        {
            Key = kvp.Key,
            Value = kvp.Value
        }
            )
            .ToList();

        await c.TagResourceAsync(new TagResourceRequest
        {
            ResourceArn = resourceArn,
            Tags = tags
        }).ConfigureAwait(false);
    }

    public static async System.Threading.Tasks.Task ClearMetadata(this IAmazonECS c, string resourceArn)
    {
        await c.UntagResourceAsync(new UntagResourceRequest
        {
            ResourceArn = resourceArn,
            TagKeys = new List<string>(ProtoLabels.AllLabels)
        }).ConfigureAwait(false);
    }
}
