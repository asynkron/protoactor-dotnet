// -----------------------------------------------------------------------
// <copyright file="ObjectPoolBenchmarks.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Concurrent;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;

namespace ClusterMicroBenchmarks
{
    [MemoryDiagnoser, InProcess]
    public class ObjectPoolBenchmarks
    {
        [Params(1000, 5000)]
        public int Items { get; set; }

        private ConcurrentBag<object> Bag { get; set; }
        private ChannelWriter<object> ChannelWriter { get; set; }
        private ChannelReader<object> ChannelReader { get; set; }
        private object[] Objects { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            Bag = new ConcurrentBag<object>();
            Objects = new object[Items];
            var channel = System.Threading.Channels.Channel.CreateUnbounded<object>();
            ChannelWriter = channel.Writer;
            ChannelReader = channel.Reader;

            for (var i = 0; i < Items; i++)
            {
                object obj = new();
                Bag.Add(obj);
                ChannelWriter.TryWrite(obj);
            }
        }

        [Benchmark]
        public void Channel()
        {
            for (var i = 0; i < Items; i++)
            {
                ChannelReader.TryRead(out var obj);
                Objects[i] = obj;
            }

            for (var i = 0; i < Items; i++)
            {
                ChannelWriter.TryWrite(Objects[i]);
            }
        }

        [Benchmark]
        public void ConcurrentBag()
        {
            for (var i = 0; i < Items; i++)
            {
                Bag.TryTake(out var obj);
                Objects[i] = obj;
            }

            for (var i = 0; i < Items; i++)
            {
                Bag.Add(Objects[i]);
            }
        }
    }
}