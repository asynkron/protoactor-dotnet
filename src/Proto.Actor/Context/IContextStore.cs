// -----------------------------------------------------------------------
// <copyright file="IReceiverContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace
namespace Proto
{
    public interface IContextStore
    {
        T? Get<T>();

        void Set<T>(T obj) => Set<T, T>(obj);

        void Set<T, TI>(TI obj) where TI : T;

        void Remove<T>();
    }
}