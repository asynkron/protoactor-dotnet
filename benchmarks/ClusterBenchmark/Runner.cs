// -----------------------------------------------------------------------
// <copyright file="Runner.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Proto.Cluster;

namespace ClusterExperiment1
{
    public interface IRunMember
    {
        Task Start();

        Task Kill();
    }

    public class RunMemberInProc : IRunMember
    {
        private Cluster _cluster;

        public async Task Start() => _cluster = await Configuration.SpawnMember();

        public async Task Kill() => await _cluster.ShutdownAsync(false);
    }

    public class RunMemberExternalProc : IRunMember
    {
        public static readonly string SelfPath = typeof(Program).GetType().Assembly.Location;
        private Process _process;

        public Task Start()
        {
            Console.WriteLine("Starting external worker");
            var l = typeof(Program).Assembly.Location;
            _process = Process.Start("dotnet", $"{l} worker");
            return Task.CompletedTask;
        }

        public Task Kill()
        {
            Console.WriteLine("Killing external worker");
            _process.Kill(true);
            return Task.CompletedTask;
        }
    }
}