// -----------------------------------------------------------------------
//   <copyright file="IClusterProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

namespace Proto.Cluster.IdentityLookup
{
    public interface IIdentityLookup
    {
        void Configure(ActorSystem system);
        Task<PID> LookupIdentity(string identity);
    }
}