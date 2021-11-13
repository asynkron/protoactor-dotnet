// -----------------------------------------------------------------------
// <copyright file="Option.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Streams
{
    public class Option<T>
    {
        public static readonly Option<T> None;
        public class NoneImpl : Option<T> {}
    }

    public class Result<T1>
    {
        
    }
    public class Result<T1, T2>
    {
        
    }

    public class Either<T1, T2>
    {
        
    }

    public interface ICancelable
    {
        
    }

    public interface ILoggingAdapter
    {
        
    }
}