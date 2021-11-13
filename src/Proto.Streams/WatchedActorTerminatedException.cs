//-----------------------------------------------------------------------
// <copyright file="WatchedActorTerminatedException.cs" company="Proto.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Runtime.Serialization;
using Proto.Streams.Dsl;

namespace Proto.Streams
{
    /// <summary>
    /// Used as failure exception by an `ask` operator if the target actor terminates.
    /// </summary>
    /// <seealso cref="Flow{TIn,TOut,TMat}.Ask{TOut2}"/>
    /// <seealso cref="Source{TOut,TMat}.Ask{TOut2}"/>
    /// <seealso cref="FlowOperations.Watch{T,TMat}"/>
    /// <seealso cref="SourceOperations.Watch{T,TMat}"/>
    public class WatchedActorTerminatedException : Exception
    {
        public WatchedActorTerminatedException(string stageName, PID pid) 
            : base($"Actor watched by [{stageName}] has terminated! Was: {pid}")
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProtoException"/> class.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
        protected WatchedActorTerminatedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
