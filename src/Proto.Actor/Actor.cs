// -----------------------------------------------------------------------
//  <copyright file="Actor.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Proto
{
    public delegate Task Receive(IContext context);

    public delegate Task Sender(IContext ctx, PID target, MessageEnvelope envelope);

    public class EmptyActor : IActor
    {
        private readonly Receive _receive;

        public EmptyActor(Receive receive)
        {
            _receive = receive;
        }

        public Task ReceiveAsync(IContext context)
        {
            return _receive(context);
        }
    }

    public static class Actor
    {
        public static readonly Task Done = Task.FromResult(0);

        public static EventStream EventStream => EventStream.Instance;

        public static Props FromProducer(Func<IActor> producer)
        {
            return new Props().WithProducer(producer);
        }

        public static Props FromFunc(Receive receive)
        {
            return FromProducer(() => new EmptyActor(receive));
        }

        public static PID Spawn(Props props)
        {
            var name = ProcessRegistry.Instance.NextId();
            return SpawnNamed(props, name);
        }

        public static PID SpawnPrefix(Props props, string prefix)
        {
            var name = prefix + ProcessRegistry.Instance.NextId();
            return SpawnNamed(props, name);
        }

        public static PID SpawnNamed(Props props, string name)
        {
            return props.Spawn(name, null);
        }
    }

    public class ProcessNameExistException : Exception
    {
        private string _name;

        public ProcessNameExistException(string name)
        {
            _name = name;
        }
    }

    public interface IActor
    {
        Task ReceiveAsync(IContext context);
    }
}