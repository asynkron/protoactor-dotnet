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
    /// Retrieves the next system message of type <typeparamref name="T"/>.
    /// </summary>
    public static T ExpectSystemMessage<T>(this ITestProbe probe, TimeSpan? timeAllowed = null)
        where T : SystemMessage => probe.GetNextMessage<T>(timeAllowed);

    /// <summary>
    /// Asynchronously retrieves the next system message of type <typeparamref name="T"/>.
    /// </summary>
    public static Task<T> ExpectSystemMessageAsync<T>(this ITestProbe probe, TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default)
        where T : SystemMessage => probe.GetNextMessageAsync<T>(timeAllowed, cancellationToken);
}
