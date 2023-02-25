using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Identity.Tests;

public abstract class IdentityStorageTests : IDisposable
{
    private const int TimeoutMs = 2000;
    private static int testId = 1;
    private readonly IIdentityStorage _storage;
    private readonly IIdentityStorage _storageInstance2;
    private readonly ITestOutputHelper _testOutputHelper;

    protected IdentityStorageTests(
        Func<string, IIdentityStorage> storageFactory,
        ITestOutputHelper testOutputHelper
    )
    {
        _testOutputHelper = testOutputHelper;
        var clusterName = $"test-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        _storage = storageFactory(clusterName);
        _storageInstance2 = storageFactory(clusterName);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _storage?.Dispose();
        _storageInstance2?.Dispose();
    }

    [Fact]
    public async Task GlobalLockActivatesOnceOnly()
    {
        var timeout = new CancellationTokenSource(TimeoutMs).Token;
        var identity = new ClusterIdentity { Kind = "thing", Identity = NextId().ToString() };
        const int attempts = 10;

        var locks = await Task.WhenAll(Enumerable.Range(1, attempts)
            .Select(i => _storage.TryAcquireLock(identity, timeout))
        ).ConfigureAwait(false);

        var successFullLock = locks.Where(it => it != null).ToList();
        successFullLock.Should().HaveCount(1);
        successFullLock.Single()!.ClusterIdentity.Should().BeEquivalentTo(identity);
    }

    [Fact]
    public async Task GlobalLockActivatesOnceOnlyAcrossMultipleClients()
    {
        var timeout = new CancellationTokenSource(TimeoutMs).Token;
        var identity = new ClusterIdentity { Kind = "thing", Identity = "1234" };
        const int attempts = 10;

        var locks = await Task.WhenAll(Enumerable.Range(1, attempts)
            .SelectMany(_ => new[]
                {
                    _storage.TryAcquireLock(identity, timeout),
                    _storageInstance2.TryAcquireLock(identity, timeout)
                }
            )
        ).ConfigureAwait(false);

        var successfulLock = locks.Where(it => it != null).ToList();
        successfulLock.Should().HaveCount(1);
        successfulLock.Single()!.ClusterIdentity.Should().BeEquivalentTo(identity);
    }

    [Fact]
    public async Task CannotTakeLockWhenAlreadyActivated()
    {
        var activator = GetFakeActivator();
        var timeout = new CancellationTokenSource(TimeoutMs).Token;
        var identity = new ClusterIdentity { Kind = "thing", Identity = NextId().ToString() };
        var spawnLock = await _storage.TryAcquireLock(identity, timeout).ConfigureAwait(false);
        var pid = Activate(activator, identity);
        await _storage.StoreActivation(activator.Id, spawnLock!, pid, timeout).ConfigureAwait(false);

        var activation = await _storage.TryGetExistingActivation(identity, timeout).ConfigureAwait(false);

        activation.Should().NotBeNull();
        activation?.MemberId.Should().Be(activator.Id);
        activation?.Pid.Should().BeEquivalentTo(pid);

        var noLock = await _storage.TryAcquireLock(identity, timeout).ConfigureAwait(false);

        noLock.Should().BeNull("Since the activation is active, it should not be possible to take the lock");
    }

    [Fact]
    public async Task CanDeleteSpawnLocks()
    {
        var timeout = new CancellationTokenSource(TimeoutMs).Token;
        var identity = new ClusterIdentity { Kind = "thing", Identity = NextId().ToString() };

        var spawnLock = await _storage.TryAcquireLock(identity, timeout).ConfigureAwait(false);

        spawnLock.Should().NotBeNull();

        await _storage.RemoveLock(spawnLock!, timeout).ConfigureAwait(false);

        var secondLock = await _storage.TryAcquireLock(identity, timeout).ConfigureAwait(false);

        secondLock.Should().NotBeNull("The initial lock should be cleared, and a second lock can be acquired.");
    }

    [Fact]
    public async Task CanStoreActivation()
    {
        var timeout = new CancellationTokenSource(TimeoutMs).Token;
        var (activator, identity, pid) = await GetActivatedClusterIdentity(timeout).ConfigureAwait(false);

        var activation = await _storage.TryGetExistingActivation(identity, timeout).ConfigureAwait(false);

        activation.Should().NotBeNull();
        activation?.MemberId.Should().Be(activator.Id);
        activation?.Pid.Should().BeEquivalentTo(pid);
    }

    [Fact]
    public async Task CannotStoreOverExisting()
    {
        var timeout = new CancellationTokenSource(TimeoutMs).Token;
        var (activator, identity, _) = await GetActivatedClusterIdentity(timeout).ConfigureAwait(false);

        var otherPid = Activate(activator, identity);

        await _storage.Invoking(storage =>
                storage.StoreActivation(activator.Id, new SpawnLock("someLockId", identity), otherPid, timeout)
            )
            .Should()
            .ThrowAsync<LockNotFoundException>().ConfigureAwait(false);
    }

    [Fact]
    public async Task CannotStoreWithoutLock()
    {
        var timeout = new CancellationTokenSource(TimeoutMs).Token;
        var activator = GetFakeActivator();
        var identity = new ClusterIdentity { Kind = "thing", Identity = NextId().ToString() };
        var spawnLock = new SpawnLock("not-a-lock", identity);
        var pid = Activate(activator, identity);

        await _storage.Invoking(storage =>
                storage.StoreActivation(activator.Id, spawnLock, pid, timeout)
            )
            .Should()
            .ThrowAsync<LockNotFoundException>().ConfigureAwait(false);
    }

    [Fact]
    public async Task CanRemoveActivation()
    {
        var timeout = new CancellationTokenSource(TimeoutMs).Token;
        var (activator, identity, pid) = await GetActivatedClusterIdentity(timeout).ConfigureAwait(false);

        var activation = await _storage.TryGetExistingActivation(identity, timeout).ConfigureAwait(false);

        await _storage.RemoveActivation(identity, pid, timeout).ConfigureAwait(false);

        var afterRemoval = await _storage.TryGetExistingActivation(identity, timeout).ConfigureAwait(false);

        activation.Should().NotBeNull();
        activation?.MemberId.Should().Be(activator.Id);
        activation?.Pid.Should().BeEquivalentTo(pid);

        afterRemoval.Should().BeNull();
    }

    [Fact]
    public async Task DoesNotRemoveIfIdDoesNotMatch()
    {
        var timeout = new CancellationTokenSource(TimeoutMs).Token;
        var (activator, identity, pid) = await GetActivatedClusterIdentity(timeout).ConfigureAwait(false);

        var differentPid = Activate(activator, identity);

        var activation = await _storage.TryGetExistingActivation(identity, timeout).ConfigureAwait(false);

        await _storage.RemoveActivation(identity, differentPid, timeout).ConfigureAwait(false);

        var afterRemoval = await _storage.TryGetExistingActivation(identity, timeout).ConfigureAwait(false);

        activation.Should().NotBeNull();
        activation?.MemberId.Should().Be(activator.Id);
        activation?.Pid.Should().BeEquivalentTo(pid);

        afterRemoval.Should().NotBeNull("Removal pid did not match id, even if it matched cluster identity");
    }

    [Fact]
    public async Task CanRemoveByMember()
    {
        var timeout = new CancellationTokenSource(TimeoutMs).Token;
        var (activator, identity, _) = await GetActivatedClusterIdentity(timeout).ConfigureAwait(false);

        await _storage.RemoveMember(activator.Id, timeout).ConfigureAwait(false);

        var storedActivation = await _storage.TryGetExistingActivation(identity, timeout).ConfigureAwait(false);

        storedActivation.Should().BeNull();
    }

    [Fact]
    public async Task WillNotRemoveCurrentActivationByPrevMember()
    {
        var timeout = new CancellationTokenSource(TimeoutMs).Token;
        var (originalActivator, identity, origPid) = await GetActivatedClusterIdentity(timeout).ConfigureAwait(false);

        await _storage.RemoveActivation(identity, origPid, timeout).ConfigureAwait(false);

        var (newActivator, _, newPid) = await GetActivatedClusterIdentity(timeout, identity: identity).ConfigureAwait(false);

        await _storage.RemoveMember(originalActivator.Id, timeout).ConfigureAwait(false);

        var activation = await _storage.TryGetExistingActivation(identity, timeout).ConfigureAwait(false);

        activation.Should().NotBeNull();
        activation?.MemberId.Should().Be(newActivator.Id);
        activation?.Pid.Should().BeEquivalentTo(newPid);
    }

    [Theory]
    [InlineData(200, 10000)]
    public async Task CanRemoveMemberWithManyActivations(int activations, int msTimeout)
    {
        var identities = new List<ClusterIdentity>();
        var timeout = new CancellationTokenSource(msTimeout).Token;
        var activator = GetFakeActivator();

        for (var i = 0; i < activations; i++)
        {
            var (_, identity, _) = await GetActivatedClusterIdentity(timeout, activator).ConfigureAwait(false);
            identities.Add(identity);
        }

        var timer = Stopwatch.StartNew();
        await _storage.RemoveMember(activator.Id, timeout).ConfigureAwait(false);
        timer.Stop();
        _testOutputHelper.WriteLine($"Removed {activations} activations in {timer.Elapsed}");

        foreach (var clusterIdentity in identities)
        {
            var storedActivation = await _storage.TryGetExistingActivation(clusterIdentity, timeout).ConfigureAwait(false);
            storedActivation.Should().BeNull();
        }
    }

    private async Task<(Member, ClusterIdentity, PID activation)> GetActivatedClusterIdentity(
        CancellationToken timeout,
        Member? activator = null,
        ClusterIdentity? identity = null
    )
    {
        activator ??= GetFakeActivator();
        identity ??= new ClusterIdentity { Kind = "thing", Identity = NextId().ToString() };
        var spawnLock = await _storage.TryAcquireLock(identity, timeout).ConfigureAwait(false);
        var pid = Activate(activator, identity);
        await _storage.StoreActivation(activator.Id, spawnLock!, pid, timeout).ConfigureAwait(false);

        var activation = await _storage.TryGetExistingActivation(identity, timeout).ConfigureAwait(false);

        return (activator, identity, activation!.Pid);
    }

    [Fact]
    public async Task CanWaitForActivation()
    {
        var activator = GetFakeActivator();
        var timeout = CancellationTokens.FromSeconds(15);
        var identity = new ClusterIdentity { Kind = "thing", Identity = NextId().ToString() };
        var spawnLock = await _storage.TryAcquireLock(identity, timeout).ConfigureAwait(false);
        var pid = Activate(activator, identity);

        _ = SafeTask.Run(async () =>
            {
                await Task.Delay(500, timeout).ConfigureAwait(false);
                await _storage.StoreActivation(activator.Id, spawnLock!, pid, timeout).ConfigureAwait(false);
            }, timeout
        );

        var activation = await _storage.WaitForActivation(identity, timeout).ConfigureAwait(false);

        activation.Should().NotBeNull();
        activation?.MemberId.Should().Be(activator.Id);
        activation?.Pid.Should().BeEquivalentTo(pid);
    }

    [Fact]
    public async Task RemovesLockIfStale()
    {
        var timeout = new CancellationTokenSource(10000).Token;
        var identity = new ClusterIdentity { Kind = "thing", Identity = NextId().ToString() };
        await _storage.TryAcquireLock(identity, timeout).ConfigureAwait(false);

        var activation = await _storage.WaitForActivation(identity, timeout).ConfigureAwait(false);
        var spawnLock = await _storage.TryAcquireLock(identity, timeout).ConfigureAwait(false);

        activation.Should().BeNull("We did not activate it");

        spawnLock.Should()
            .NotBeNull(
                "When an activation did not occur, the storage implementation should discard the lock"
            );
    }

    // ReSharper disable once SuggestBaseTypeForParameter
    private PID Activate(Member activator, ClusterIdentity identity) =>
        PID.FromAddress(activator.Address, $"placement-activator/{identity}${NextId()}");

    private Member GetFakeActivator()
    {
        var activator = new Member
        {
            Host = "127.0.0.1",
            Port = NextId(),
            Id = Guid.NewGuid().ToString(),
            Kinds = { "thing" }
        };

        return activator;
    }

    private int NextId() => Interlocked.Increment(ref testId);
}