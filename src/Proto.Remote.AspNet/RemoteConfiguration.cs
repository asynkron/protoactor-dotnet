// -----------------------------------------------------------------------
//   <copyright file="RemoteConfiguration.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Remote
{
    public class RemoteConfiguration
    {
        public RemoteConfiguration(Serialization serialization, RemoteKindRegistry remoteKindRegistry, AspRemoteConfig remoteConfig)
        {
            Serialization = serialization;
            RemoteKindRegistry = remoteKindRegistry;
            RemoteConfig = remoteConfig;
        }

        public Serialization Serialization { get; }
        public RemoteKindRegistry RemoteKindRegistry { get; }
        public AspRemoteConfig RemoteConfig { get; }
    }
}