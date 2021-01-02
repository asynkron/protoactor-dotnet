// -----------------------------------------------------------------------
// <copyright file="DockerSupport.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers.Builders;
using DotNet.Testcontainers.Containers.Modules;

namespace ClusterExperiment1
{
    public class DockerSupport
    {
        public static async Task Run(Task done)
        {
            var consulBuilder = new TestcontainersBuilder<TestcontainersContainer>()
                .WithImage("consul:latest")
                .WithName("consul")
                .WithPortBinding("8500", "8500")
                .WithPortBinding("8600", "8600/udp")
                .WithCommand("agent -server -bootstrap -ui -client=0.0.0.0");
            
            await using var consul = consulBuilder.Build();
            await consul.StartAsync();
            
            Console.WriteLine("started consul");
            
            var mongoBuilder = new TestcontainersBuilder<TestcontainersContainer>()
                .WithImage("mongo")
                .WithName("mongo")
                .WithPortBinding("27017", "27017")
                .WithMount(".", "/data/db");
            
            await using var mongo = mongoBuilder.Build();
            await mongo.StartAsync();
            
            Console.WriteLine("started mongo");

            await done;
        }
    }
}