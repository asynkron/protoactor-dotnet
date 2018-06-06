// -----------------------------------------------------------------------
//   <copyright file="Actor.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Proto
{
    

    public static class Actor
    {
        public static readonly Task Done = Task.FromResult(0);
        public static EventStream EventStream => EventStream.Instance;
        public static Props FromProducer(Func<IActor> producer) => new Props().WithProducer(producer);
        public static Props FromFunc(Receive receive) => FromProducer(() => new EmptyActor(receive));

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
            var parent = props.GuardianStrategy != null ? Guardians.GetGuardianPID(props.GuardianStrategy) : null;
            return props.Spawn(name, parent);
        }
    }
}