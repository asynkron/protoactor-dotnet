// -----------------------------------------------------------------------
// <copyright file="CancellationTokens.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Threading;
using Proto.Utils;

namespace Proto
{
    
    public static class CancellationTokens
    {
        private record TokenEntry(DateTimeOffset timestamp, CancellationToken token);
        
        private static readonly ConcurrentDictionary<int, TokenEntry> Tokens = new();

        public static CancellationToken FromSeconds(int seconds)
        {
            if (seconds < 1) throw new ArgumentOutOfRangeException(nameof(seconds));
            
            static TokenEntry ValueFactory(int seconds)
            {
                var cts = new CancellationTokenSource(seconds * 1000);
                return new TokenEntry(DateTimeOffset.Now, cts.Token);
            }

            while (true)
            {
                var x = Tokens.GetOrAdd(seconds, ValueFactory)!;

                //is the entry expired?
                //token reuse expire after 500ms
                if (x.timestamp >= DateTimeOffset.Now.AddMilliseconds(-500))
                {
                    return x.token;
                }

                Tokens.TryRemove(seconds, out _);
            }
        }
        public static CancellationToken WithTimeout(int ms) => new CancellationTokenSource(ms).Token;
        public static CancellationToken WithTimeout(TimeSpan timeSpan) => new CancellationTokenSource(timeSpan).Token;
    }
}