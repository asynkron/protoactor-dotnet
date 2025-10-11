using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Utils;
using Xunit;

namespace Proto.Tests.Utils;

public class RetryTests
{
    [Fact]
    public async Task Try_ShouldRespectCancellationToken()
    {
        var attempts = 0;
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(20); // cancel to stop retry loop early

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await Retry.Try(
                () =>
                {
                    attempts++;
                    throw new Exception("boom");
                },
                retryCount: 100,
                backoffMilliSeconds: 1,
                maxBackoffMilliseconds: 1,
                ct: cts.Token));
        attempts.Should().BeLessThan(100);
    }

    [Fact]
    public async Task TryUntilNotNull_ShouldReturnValueOnceAvailable()
    {
        var attempts = 0;

        var result = await Retry.TryUntilNotNull<string>(
            () =>
            {
                attempts++;
                return Task.FromResult(attempts >= 3 ? "ok" : null!);
            },
            retryCount: 5,
            backoffMilliSeconds: 1,
            maxBackoffMilliseconds: 1);

        result.Should().Be("ok");
        attempts.Should().Be(3);
    }
}
