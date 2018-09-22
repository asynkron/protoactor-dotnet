// -----------------------------------------------------------------------
//   <copyright file="Log.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace Proto
{
    public static class Log
    {
        private static ILoggerFactory _loggerFactory = new NullLoggerFactory();

        public static void SetLoggerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public static ILogger CreateLogger(string categoryName) => _loggerFactory.CreateLogger(categoryName);

        public static ILogger CreateLogger<T>() => _loggerFactory.CreateLogger<T>();
    }
}