// -----------------------------------------------------------------------
// <copyright file="DockerSupport.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.IO;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers.Builders;
using DotNet.Testcontainers.Containers.Modules;
using DotNet.Testcontainers.Containers.WaitStrategies;

namespace ClusterExperiment1
{
    public class DockerSupport
    {
        public static async Task Run(Task done)
        {
            Directory.CreateDirectory("_data/db");
            var mongoBuilder = new TestcontainersBuilder<TestcontainersContainer>()
                .WithImage("mongo")
                .WithCleanUp(true)
                .WithName("mongo")
                .WithPortBinding("27017", "27017");

            await using var mongo = mongoBuilder.Build();
            await mongo.StartAsync();

            Console.WriteLine("started mongo");

            var consulBuilder = new TestcontainersBuilder<TestcontainersContainer>()
                .WithImage("consul:latest")
                .WithCleanUp(true)
                .WithName("consul")
                .WithPortBinding("8500", "8500")
                .WithPortBinding("8600", "8600/udp");

            await using var consul = consulBuilder.Build();

            await consul.StartAsync();

            Console.WriteLine("started consul");

            await done;
            Console.WriteLine("Exited.......");
        }
    }
}