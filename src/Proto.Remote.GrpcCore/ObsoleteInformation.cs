// -----------------------------------------------------------------------
// <copyright file = "ObsoleteInformation.cs" company = "Asynkron AB">
//      Copyright (C) 2015-$CURRENT_YEAR$ Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Proto.Remote.GrpcCore
{
    /// <summary>
    /// Proto.Remote.GrpcCore will be removed in one of the next releases.
    /// It is because Grpc.Core nuget package is in maintenance mode and Grpc.Net.Client is recommended.
    /// https://grpc.io/blog/grpc-csharp-future/ 
    /// Use Proto.Remote.GrpcNet as a replacement.
    /// </summary>
    internal class ObsoleteInformation
    {
        public const string Text = "Use Proto.Remote.GrpcNet as a replacement. Proto.Remote.GrpcCore will be removed in one of the next releases";
    }
}
