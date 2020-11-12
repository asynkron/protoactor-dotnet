using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Proto.Cluster.Identity.Tests
{
    public abstract class IdentityStorageTests : IDisposable
    {
        private readonly IIdentityStorage _storage;
        private readonly IIdentityStorage _storageInstance2;
        private static int _testId = 1;

        protected IdentityStorageTests(Func<string, IIdentityStorage> storageFactory)
        {
            var clusterName = "test-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            _storage = storageFactory(clusterName);
            _storageInstance2 = storageFactory(clusterName);
        }

        [Fact]
        public async Task GlobalLockActivatesOnceOnly()
        {
            var timeout = new CancellationTokenSource(1000).Token;
            var identity = new ClusterIdentity {Kind = "thing", Identity = NextId().ToString()};
            const int attempts = 10;

            var locks = await Task.WhenAll(Enumerable.Range(1, attempts)
                .Select(i => _storage.TryAcquireLockAsync(identity, timeout))
            );

            var successFullLock = locks.Where(it => it != null).ToList();
            successFullLock.Should().HaveCount(1);
            successFullLock.Single()!.ClusterIdentity.Should().BeEquivalentTo(identity);
        }

        [Fact]
        public async Task GlobalLockActivatesOnceOnlyAcrossMultipleClients()
        {
            var timeout = new CancellationTokenSource(1000).Token;
            var identity = new ClusterIdentity {Kind = "thing", Identity = "1234"};
            const int attempts = 10;

            var locks = await Task.WhenAll(Enumerable.Range(1, attempts)
                .SelectMany(i => new[]
                    {
                        _storage.TryAcquireLockAsync(identity, timeout),
                        _storageInstance2.TryAcquireLockAsync(identity, timeout)
                    }
                )
            );

            var successFullLock = locks.Where(it => it != null).ToList();
            successFullLock.Should().HaveCount(1);
            successFullLock.Single()!.ClusterIdentity.Should().BeEquivalentTo(identity);
        }

        [Fact]
        public async Task CannotTakeLockWhenAlreadyActivated()
        {
            var activator = GetFakeActivator();
            var timeout = new CancellationTokenSource(1000).Token;
            var identity = new ClusterIdentity {Kind = "thing", Identity = NextId().ToString()};
            var spawnLock = await _storage.TryAcquireLockAsync(identity, timeout);
            var pid = Activate(activator, identity);
            await _storage.StoreActivation(activator.Id, spawnLock!, pid, timeout);

            var activation = await _storage.TryGetExistingActivationAsync(identity, timeout);

            activation.Should().NotBeNull();
            activation!.MemberId.Should().Be(activator.Id);
            activation!.Pid.Should().BeEquivalentTo(pid);

            var noLock = await _storage.TryAcquireLockAsync(identity, timeout);

            noLock.Should().BeNull("Since the activation is active, it should not be possible to take the lock");
        }

        [Fact]
        public async Task CanDeleteSpawnLocks()
        {
            var timeout = new CancellationTokenSource(1000).Token;
            var identity = new ClusterIdentity {Kind = "thing", Identity = NextId().ToString()};

            var spawnLock = await _storage.TryAcquireLockAsync(identity, timeout);

            spawnLock.Should().NotBeNull();

            await _storage.RemoveLock(spawnLock!, timeout);

            var secondLock = await _storage.TryAcquireLockAsync(identity, timeout);

            secondLock.Should().NotBeNull("The initial lock should be cleared, and a second lock can be acquired.");
        }

        [Fact]
        public async Task CanStoreActivation()
        {
            var timeout = new CancellationTokenSource(1000).Token;
            var (activator, identity, pid) = await GetActivatedClusterIdentity(timeout);

            var activation = await _storage.TryGetExistingActivationAsync(identity, timeout);

            activation.Should().NotBeNull();
            activation!.MemberId.Should().Be(activator.Id);
            activation!.Pid.Should().BeEquivalentTo(pid);
        }

        [Fact]
        public async Task CanRemoveActivation()
        {
            var timeout = new CancellationTokenSource(1000).Token;
            var (activator, identity, pid) = await GetActivatedClusterIdentity(timeout);

            var activation = await _storage.TryGetExistingActivationAsync(identity, timeout);

            await _storage.RemoveActivation(pid, timeout);

            var afterRemoval = await _storage.TryGetExistingActivationAsync(identity, timeout);


            activation.Should().NotBeNull();
            activation!.MemberId.Should().Be(activator.Id);
            activation!.Pid.Should().BeEquivalentTo(pid);

            afterRemoval.Should().BeNull();
        }

        [Fact]
        public async Task DoesNotRemoveIfIdDoesNotMatch()
        {
            var timeout = new CancellationTokenSource(1000).Token;
            var (activator, identity, pid) = await GetActivatedClusterIdentity(timeout);


            var differentPid = Activate(activator, identity);

            var activation = await _storage.TryGetExistingActivationAsync(identity, timeout);

            await _storage.RemoveActivation(differentPid, timeout);

            var afterRemoval = await _storage.TryGetExistingActivationAsync(identity, timeout);


            activation.Should().NotBeNull();
            activation!.MemberId.Should().Be(activator.Id);
            activation!.Pid.Should().BeEquivalentTo(pid);

            afterRemoval.Should().NotBeNull("Removal pid did not match id, even if it matched cluster identity");
        }
        
        [Fact]
        public async Task CanRemoveByMember()
        {
            var timeout = new CancellationTokenSource(1000).Token;
            var (activator, identity, _) = await GetActivatedClusterIdentity(timeout);


            await _storage.RemoveMemberIdAsync(activator.Id, timeout);

            var storedActivation = await _storage.TryGetExistingActivationAsync(identity, timeout);

            storedActivation.Should().BeNull();
        }

        private async Task<(Member, ClusterIdentity, PID activation)> GetActivatedClusterIdentity(
            CancellationToken timeout)
        {
            var activator = GetFakeActivator();
            var identity = new ClusterIdentity {Kind = "thing", Identity = NextId().ToString()};
            var spawnLock = await _storage.TryAcquireLockAsync(identity, timeout);
            var pid = Activate(activator, identity);
            await _storage.StoreActivation(activator.Id, spawnLock!, pid, timeout);

            var activation = await _storage.TryGetExistingActivationAsync(identity, timeout);

            return (activator, identity, activation!.Pid);
        }

        [Fact]
        public async Task CanWaitForActivation()
        {
            var activator = GetFakeActivator();
            var timeout = new CancellationTokenSource(5000).Token;
            var identity = new ClusterIdentity {Kind = "thing", Identity = NextId().ToString()};
            var spawnLock = await _storage.TryAcquireLockAsync(identity, timeout);
            var pid = Activate(activator, identity);

            _ = Task.Run(async () =>
                {
                    await Task.Delay(1000, timeout);
                    await _storage.StoreActivation(activator.Id, spawnLock!, pid, timeout);
                }, timeout
            );


            var activation = await _storage.WaitForActivationAsync(identity, timeout);

            activation.Should().NotBeNull();
            activation!.MemberId.Should().Be(activator.Id);
            activation!.Pid.Should().BeEquivalentTo(pid);
        }


        private PID Activate(Member activator, ClusterIdentity identity)
        {
            return PID.FromAddress(activator.Address, "placement-activator/" + identity.ToShortString() + "$" + NextId());
        }

        private Member GetFakeActivator()
        {
            Member activator = new Member
            {
                Host = "127.0.0.1",
                Port = NextId(),
                Id = Guid.NewGuid().ToString(),
                Kinds = {"thing"}
            };
            return activator;
        }

        private int NextId()
        {
            return Interlocked.Increment(ref _testId);
        }

        public void Dispose()
        {
            _storage?.Dispose();
            _storageInstance2?.Dispose();
        }
    }


}