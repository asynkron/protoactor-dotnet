// -----------------------------------------------------------------------
// <copyright file="Runner.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;
using Proto.Cluster;

namespace ClusterExperiment1;

public interface IRunMember
{
    Task Start();

    Task Kill();
}

public class RunMemberInProcGraceful : IRunMember
{
    private Cluster? _cluster;

    public async Task Start() => _cluster = await Configuration.SpawnMember();

    public async Task Kill() => await _cluster!.ShutdownAsync();
}