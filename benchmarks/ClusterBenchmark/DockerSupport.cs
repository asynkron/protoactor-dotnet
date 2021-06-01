// -----------------------------------------------------------------------
// <copyright file="DockerSupport.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;

namespace ClusterExperiment1
{
    public static  class DockerSupport
    {
        public static async Task Run(Task done)
        {
            // Directory.CreateDirectory("data/db");
            // Directory.CreateDirectory("redis");
            // var mongoBuilder = new TestcontainersBuilder<TestcontainersContainer>()
            //     .WithImage("mongo")
            //     .WithCleanUp(true)
            //     .WithName("mongo")
            //     .WithPortBinding("27017", "27017");
            //
            // await using var mongo = mongoBuilder.Build();
            // await mongo.StartAsync();
            //
            // Console.WriteLine("started mongo");
            //
            // var consulBuilder = new TestcontainersBuilder<TestcontainersContainer>()
            //     .WithImage("consul:latest")
            //     .WithCleanUp(true)
            //     .WithName("consul")
            //     .WithPortBinding("8500", "8500")
            //     .WithPortBinding("8600", "8600/udp");
            //
            // await using var consul = consulBuilder.Build();
            //
            // await consul.StartAsync();
            //
            // Console.WriteLine("started consul");
            //
            //
            // var redisBuilder = new TestcontainersBuilder<TestcontainersContainer>()
            //     .WithImage("bitnami/redis:latest")
            //     .WithEnvironment("ALLOW_EMPTY_PASSWORD=yes", "yes")
            //     .WithCleanUp(true)
            //     .WithName("redis")
            //     .WithPortBinding("6379", "6379")
            //     .WithMount("redis", "/data");
            //
            // await using var redis = redisBuilder.Build();
            //
            // await redis.StartAsync();
            //
            // Console.WriteLine("started redis");

            await done;
            Console.WriteLine("Exited.......");
        }
    }
}