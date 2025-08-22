using System;
using System.Threading;
using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto.TestKit;

/// <summary>
/// Convenience methods for expecting <see cref="Proto.SystemMessage"/> instances.
/// </summary>
public static class TestProbeSystemMessageExtensions
{
    /// <summary>
    /// Asynchronously retrieves the next system message of type <typeparamref name="T"/>.
    /// </summary>
    public static Task<T> ExpectSystemMessageAsync<T>(this ITestProbe probe, TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default)
        where T : SystemMessage => probe.GetNextMessageAsync<T>(timeAllowed, cancellationToken);

    /// <summary>
    /// Asynchronously retrieves the next system message of type <typeparamref name="T"/> that satisfies
    /// the given predicate.
    /// </summary>
    public static Task<T> ExpectSystemMessageAsync<T>(this ITestProbe probe, Func<T, bool> when,
        TimeSpan? timeAllowed = null, CancellationToken cancellationToken = default)
        where T : SystemMessage => probe.GetNextMessageAsync(when, timeAllowed, cancellationToken);
}
