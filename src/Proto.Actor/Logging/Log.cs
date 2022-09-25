// -----------------------------------------------------------------------
// <copyright file="Log.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Logging;

// ReSharper disable once CheckNamespace
namespace Proto;

/// <summary>
///     Utility to create a <see cref="Microsoft.Extensions.Logging.ILogger" /> from a globally shared
///     <see cref="Microsoft.Extensions.Logging.ILoggerFactory" />.
/// </summary>
[PublicAPI]
public static class Log
{
    private static ILoggerFactory _loggerFactory = new NullLoggerFactory();

    /// <summary>
    ///     Configure the global logger factory.
    /// </summary>
    /// <param name="loggerFactory"></param>
    public static void SetLoggerFactory(ILoggerFactory loggerFactory) => _loggerFactory = loggerFactory;

    /// <summary>
    ///     Get the global logger factory.
    /// </summary>
    /// <returns></returns>
    public static ILoggerFactory GetLoggerFactory() => _loggerFactory;

    /// <summary>
    ///     Create a logger for the given category name.
    /// </summary>
    /// <param name="categoryName"></param>
    /// <returns></returns>
    public static ILogger CreateLogger(string categoryName) => _loggerFactory.CreateLogger(categoryName);

    /// <summary>
    ///     Create a logger for the given type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static ILogger CreateLogger<T>() => _loggerFactory.CreateLogger<T>();
}