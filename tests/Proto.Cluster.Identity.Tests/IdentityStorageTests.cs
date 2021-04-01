using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Identity.Tests
{
    public abstract class IdentityStorageTests : IDisposable
    {
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
            string clusterName = $"test-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
            _storage = storageFactory(clusterName);
            _storageInstance2 = storageFactory(clusterName);
        }

        public void Dispose()
        {
            _storage?.Dispose();
            _storageInstance2?.Dispose();
        }

        [Fact]
        public async Task GlobalLockActivatesOnceOnly()
        {
            CancellationToken timeout = new CancellationTokenSource(1000).Token;
            ClusterIdentity identity = new ClusterIdentity {Kind = "thing", Identity = NextId().ToString()};
            const int attempts = 10;

            SpawnLock?[] locks = await Task.WhenAll(Enumerable.Range(1, attempts)
                .Select(i => _storage.TryAcquireLock(identity, timeout))
            );

            List<SpawnLock?> successFullLock = locks.Where(it => it != null).ToList();
            successFullLock.Should().HaveCount(1);
            successFullLock.Single()!.ClusterIdentity.Should().BeEquivalentTo(identity);
        }

        [Fact]
        public async Task GlobalLockActivatesOnceOnlyAcrossMultipleClients()
        {
            CancellationToken timeout = new CancellationTokenSource(1000).Token;
            ClusterIdentity identity = new ClusterIdentity {Kind = "thing", Identity = "1234"};
            const int attempts = 10;

            SpawnLock?[] locks = await Task.WhenAll(Enumerable.Range(1, attempts)
                .SelectMany(_ => new[]
                    {
                        _storage.TryAcquireLock(identity, timeout), _storageInstance2.TryAcquireLock(identity, timeout)
                    }
                )
            );

            List<SpawnLock?> successfulLock = locks.Where(it => it != null).ToList();
            successfulLock.Should().HaveCount(1);
            successfulLock.Single()!.ClusterIdentity.Should().BeEquivalentTo(identity);
        }

        [Fact]
        public async Task CannotTakeLockWhenAlreadyActivated()
        {
            Member activator = GetFakeActivator();
            CancellationToken timeout = new CancellationTokenSource(1000).Token;
            ClusterIdentity identity = new ClusterIdentity {Kind = "thing", Identity = NextId().ToString()};
            SpawnLock? spawnLock = await _storage.TryAcquireLock(identity, timeout);
            PID pid = Activate(activator, identity);
            await _storage.StoreActivation(activator.Id, spawnLock!, pid, timeout);

            StoredActivation? activation = await _storage.TryGetExistingActivation(identity, timeout);

            activation.Should().NotBeNull();
            activation!.MemberId.Should().Be(activator.Id);
            activation!.Pid.Should().BeEquivalentTo(pid);

            SpawnLock? noLock = await _storage.TryAcquireLock(identity, timeout);

            noLock.Should().BeNull("Since the activation is active, it should not be possible to take the lock");
        }

        [Fact]
        public async Task CanDeleteSpawnLocks()
        {
            CancellationToken timeout = new CancellationTokenSource(1000).Token;
            ClusterIdentity identity = new ClusterIdentity {Kind = "thing", Identity = NextId().ToString()};

            SpawnLock? spawnLock = await _storage.TryAcquireLock(identity, timeout);

            spawnLock.Should().NotBeNull();

            await _storage.RemoveLock(spawnLock!, timeout);

            SpawnLock? secondLock = await _storage.TryAcquireLock(identity, timeout);

            secondLock.Should().NotBeNull("The initial lock should be cleared, and a second lock can be acquired.");
        }

        [Fact]
        public async Task CanStoreActivation()
        {
            CancellationToken timeout = new CancellationTokenSource(1000).Token;
            (Member activator, ClusterIdentity identity, PID pid) = await GetActivatedClusterIdentity(timeout);

            StoredActivation? activation = await _storage.TryGetExistingActivation(identity, timeout);

            activation.Should().NotBeNull();
            activation!.MemberId.Should().Be(activator.Id);
            activation!.Pid.Should().BeEquivalentTo(pid);
        }

        [Fact]
        public async Task CannotStoreOverExisting()
        {
            CancellationToken timeout = new CancellationTokenSource(1000).Token;
            (Member activator, ClusterIdentity identity, _) = await GetActivatedClusterIdentity(timeout);

            PID otherPid = Activate(activator, identity);

            _storage.Invoking(storage =>
                storage.StoreActivation(activator.Id, new SpawnLock("someLockId", identity), otherPid, timeout)
            ).Should().Throw<LockNotFoundException>();
        }

        [Fact]
        public void CannotStoreWithoutLock()
        {
            CancellationToken timeout = new CancellationTokenSource(1000).Token;
            Member activator = GetFakeActivator();
            ClusterIdentity identity = new ClusterIdentity {Kind = "thing", Identity = NextId().ToString()};
            SpawnLock spawnLock = new SpawnLock("not-a-lock", identity);
            PID pid = Activate(activator, identity);

            _storage.Invoking(storage =>
                storage.StoreActivation(activator.Id, spawnLock, pid, timeout)
            ).Should().Throw<LockNotFoundException>();
        }

        [Fact]
        public async Task CanRemoveActivation()
        {
            CancellationToken timeout = new CancellationTokenSource(1000).Token;
            (Member activator, ClusterIdentity identity, PID pid) = await GetActivatedClusterIdentity(timeout);

            StoredActivation? activation = await _storage.TryGetExistingActivation(identity, timeout);

            await _storage.RemoveActivation(pid, timeout);

            StoredActivation? afterRemoval = await _storage.TryGetExistingActivation(identity, timeout);

            activation.Should().NotBeNull();
            activation!.MemberId.Should().Be(activator.Id);
            activation!.Pid.Should().BeEquivalentTo(pid);

            afterRemoval.Should().BeNull();
        }

        [Fact]
        public async Task DoesNotRemoveIfIdDoesNotMatch()
        {
            CancellationToken timeout = new CancellationTokenSource(1000).Token;
            (Member activator, ClusterIdentity identity, PID pid) = await GetActivatedClusterIdentity(timeout);

            PID differentPid = Activate(activator, identity);

            StoredActivation? activation = await _storage.TryGetExistingActivation(identity, timeout);

            await _storage.RemoveActivation(differentPid, timeout);

            StoredActivation? afterRemoval = await _storage.TryGetExistingActivation(identity, timeout);

            activation.Should().NotBeNull();
            activation!.MemberId.Should().Be(activator.Id);
            activation!.Pid.Should().BeEquivalentTo(pid);

            afterRemoval.Should().NotBeNull("Removal pid did not match id, even if it matched cluster identity");
        }

        [Fact]
        public async Task CanRemoveByMember()
        {
            CancellationToken timeout = new CancellationTokenSource(1000).Token;
            (Member activator, ClusterIdentity identity, _) = await GetActivatedClusterIdentity(timeout);

            await _storage.RemoveMember(activator.Id, timeout);

            StoredActivation? storedActivation = await _storage.TryGetExistingActivation(identity, timeout);

            storedActivation.Should().BeNull();
        }

        [Fact]
        public async Task WillNotRemoveCurrentActivationByPrevMember()
        {
            CancellationToken timeout = new CancellationTokenSource(1000).Token;
            (Member originalActivator, ClusterIdentity identity, PID origPid) =
                await GetActivatedClusterIdentity(timeout);

            await _storage.RemoveActivation(origPid, timeout);

            (Member newActivator, _, PID newPid) = await GetActivatedClusterIdentity(timeout, identity: identity);

            await _storage.RemoveMember(originalActivator.Id, timeout);

            StoredActivation? activation = await _storage.TryGetExistingActivation(identity, timeout);

            activation.Should().NotBeNull();
            activation!.MemberId.Should().Be(newActivator.Id);
            activation!.Pid.Should().BeEquivalentTo(newPid);
        }

        [Theory]
        [InlineData(200, 10000)]
        public async Task CanRemoveMemberWithManyActivations(int activations, int msTimeout)
        {
            List<ClusterIdentity> identities = new List<ClusterIdentity>();
            CancellationToken timeout = new CancellationTokenSource(msTimeout).Token;
            Member activator = GetFakeActivator();

            for (int i = 0; i < activations; i++)
            {
                (_, ClusterIdentity identity, _) = await GetActivatedClusterIdentity(timeout, activator);
                identities.Add(identity);
            }

            Stopwatch timer = Stopwatch.StartNew();
            await _storage.RemoveMember(activator.Id, timeout);
            timer.Stop();
            _testOutputHelper.WriteLine($"Removed {activations} activations in {timer.Elapsed}");

            foreach (ClusterIdentity clusterIdentity in identities)
            {
                StoredActivation? storedActivation = await _storage.TryGetExistingActivation(clusterIdentity, timeout);
                storedActivation.Should().BeNull();
            }
        }

        private async Task<(Member, ClusterIdentity, PID activation)> GetActivatedClusterIdentity(
            CancellationToken timeout,
            Member activator = null,
            ClusterIdentity identity = null
        )
        {
            activator ??= GetFakeActivator();
            identity ??= new ClusterIdentity {Kind = "thing", Identity = NextId().ToString()};
            SpawnLock? spawnLock = await _storage.TryAcquireLock(identity, timeout);
            PID pid = Activate(activator, identity);
            await _storage.StoreActivation(activator.Id, spawnLock!, pid, timeout);

            StoredActivation? activation = await _storage.TryGetExistingActivation(identity, timeout);

            return (activator, identity, activation!.Pid);
        }

        [Fact]
        public async Task CanWaitForActivation()
        {
            Member activator = GetFakeActivator();
            CancellationToken timeout = CancellationTokens.WithTimeout(15 * 1000);
            ClusterIdentity identity = new ClusterIdentity {Kind = "thing", Identity = NextId().ToString()};
            SpawnLock? spawnLock = await _storage.TryAcquireLock(identity, timeout);
            PID pid = Activate(activator, identity);

            _ = SafeTask.Run(async () =>
                {
                    await Task.Delay(500, timeout);
                    await _storage.StoreActivation(activator.Id, spawnLock!, pid, timeout);
                }, timeout
            );
            StoredActivation? activation = await _storage.WaitForActivation(identity, timeout);

            activation.Should().NotBeNull();
            activation!.MemberId.Should().Be(activator.Id);
            activation!.Pid.Should().BeEquivalentTo(pid);
        }

        [Fact]
        public async Task RemovesLockIfStale()
        {
            CancellationToken timeout = new CancellationTokenSource(10000).Token;
            ClusterIdentity identity = new ClusterIdentity {Kind = "thing", Identity = NextId().ToString()};
            await _storage.TryAcquireLock(identity, timeout);

            StoredActivation? activation = await _storage.WaitForActivation(identity, timeout);
            SpawnLock? spawnLock = await _storage.TryAcquireLock(identity, timeout);

            activation.Should().BeNull("We did not activate it");
            spawnLock.Should().NotBeNull(
                "When an activation did not occur, the storage implementation should discard the lock"
            );
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private PID Activate(Member activator, ClusterIdentity identity) =>
            PID.FromAddress(activator.Address, $"placement-activator/{identity}${NextId()}");

        private Member GetFakeActivator()
        {
            Member activator = new Member
            {
                Host = "127.0.0.1", Port = NextId(), Id = Guid.NewGuid().ToString(), Kinds = {"thing"}
            };
            return activator;
        }

        private int NextId() => Interlocked.Increment(ref testId);
    }
}
