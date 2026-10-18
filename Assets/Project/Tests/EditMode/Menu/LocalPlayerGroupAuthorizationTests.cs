using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher.Tests.Menu
{
    public sealed class LocalPlayerGroupAuthorizationTests
    {
        [Test]
        public void ServerAssignsDistinctGroupsAndDisclosesEachSecretOnlyToItsPrimaryConnection()
        {
            using var arena = new LobbyTestArena();
            Entity first = arena.QueueJoin(700, 0);
            Entity second = arena.QueueJoin(900, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            LocalPlayerGroup firstGroup = arena.Manager.GetComponentData<LocalPlayerGroup>(first);
            LocalPlayerGroup secondGroup = arena.Manager.GetComponentData<LocalPlayerGroup>(second);
            Assert.That(firstGroup.Id, Is.Positive.And.Not.EqualTo(700));
            Assert.That(secondGroup.Id, Is.Positive.And.Not.EqualTo(firstGroup.Id));
            Assert.That(firstGroup.JoinToken.IsEmpty, Is.False);
            Assert.That(secondGroup.JoinToken, Is.Not.EqualTo(firstGroup.JoinToken));
            using EntityQuery acknowledgements = arena.Manager.CreateEntityQuery(typeof(JoinArenaAcceptedRpc), typeof(SendRpcCommandRequest));
            using NativeArray<Entity> messages = acknowledgements.ToEntityArray(Allocator.Temp);
            Assert.That(messages.Length, Is.EqualTo(2));
            foreach (Entity message in messages)
            {
                Entity recipient = arena.Manager.GetComponentData<SendRpcCommandRequest>(message).TargetConnection;
                JoinArenaAcceptedRpc accepted = arena.Manager.GetComponentData<JoinArenaAcceptedRpc>(message);
                LocalPlayerGroup recipientGroup = arena.Manager.GetComponentData<LocalPlayerGroup>(recipient);
                Assert.That(accepted.JoinToken, Is.EqualTo(recipientGroup.JoinToken));
                Assert.That(accepted.LocalPlayerGroupId, Is.EqualTo(recipientGroup.Id));
            }

            arena.LobbySystem.Update();
            using EntityQuery snapshots = arena.Manager.CreateEntityQuery(typeof(LobbySnapshotRpc));
            using NativeArray<LobbySnapshotRpc> broadcasts = snapshots.ToComponentDataArray<LobbySnapshotRpc>(Allocator.Temp);
            foreach (LobbySnapshotRpc broadcast in broadcasts)
            {
                Assert.That(broadcast.Players.ToString(), Does.Not.Contain(firstGroup.JoinToken.ToString()));
                Assert.That(broadcast.Players.ToString(), Does.Not.Contain(secondGroup.JoinToken.ToString()));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PublicGroupNumberOrUnrelatedSecretCannotJoinAnotherPlayersSharedView(bool useAnotherSecret)
        {
            using var arena = new LobbyTestArena();
            Entity victim = arena.QueueJoin(1, 0);
            Entity attacker = arena.QueueJoin(2, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            LocalPlayerGroup victimGroup = arena.Manager.GetComponentData<LocalPlayerGroup>(victim);
            FixedString64Bytes token = useAnotherSecret
                ? arena.Manager.GetComponentData<LocalPlayerGroup>(attacker).JoinToken
                : new FixedString64Bytes(victimGroup.Id.ToString());
            Entity additional = arena.QueueJoin(3, 1, token);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            if (useAnotherSecret)
            {
                Assert.That(arena.Manager.GetComponentData<LobbyMember>(additional).LocalPlayerGroupId,
                    Is.Not.EqualTo(victimGroup.Id));
            }
            else
            {
                AssertRejected(arena, additional);
            }
        }

        [Test]
        public void ValidSecretJoinsItsGroupWithoutGivingSecondaryConnectionTheSecret()
        {
            using var arena = new LobbyTestArena();
            Entity primary = arena.QueueJoin(1, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            LocalPlayerGroup group = arena.Manager.GetComponentData<LocalPlayerGroup>(primary);
            Entity additional = arena.QueueJoin(2, 1, group.JoinToken);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            Assert.That(arena.Manager.GetComponentData<LobbyMember>(additional).LocalPlayerGroupId, Is.EqualTo(group.Id));
            Assert.That(arena.Manager.HasComponent<LocalPlayerGroup>(additional), Is.False);
            using EntityQuery replies = arena.Manager.CreateEntityQuery(typeof(JoinArenaAcceptedRpc), typeof(SendRpcCommandRequest));
            using NativeArray<Entity> messages = replies.ToEntityArray(Allocator.Temp);
            Entity reply = messages.Single(message =>
                arena.Manager.GetComponentData<SendRpcCommandRequest>(message).TargetConnection == additional);
            Assert.That(arena.Manager.GetComponentData<JoinArenaAcceptedRpc>(reply).JoinToken.IsEmpty, Is.True);
        }

        [TestCase(-1)]
        [TestCase(4)]
        [TestCase(int.MaxValue)]
        [TestCase(1)]
        public void InvalidSlotOrMissingSecretIsRejected(int localIndex)
        {
            using var arena = new LobbyTestArena();
            Entity rejected = arena.QueueJoin(1, localIndex);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            AssertRejected(arena, rejected);
        }

        [Test]
        public void DuplicateSlotAndRepeatedRequestsInSameTickCreateOnlyOneAdditionalPlayer()
        {
            using var arena = new LobbyTestArena();
            Entity primary = arena.QueueJoin(1, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            FixedString64Bytes token = arena.Manager.GetComponentData<LocalPlayerGroup>(primary).JoinToken;
            Entity accepted = arena.QueueJoin(2, 1, token);
            arena.QueueJoinRequest(accepted, 1, token);
            Entity rejected = arena.QueueJoin(3, 1, token);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            AssertRejected(arena, rejected);
            using EntityQuery players = arena.Manager.CreateEntityQuery(typeof(ArenaPlayer));
            Assert.That(players.CalculateEntityCount(), Is.EqualTo(2));
            Entity occupied = arena.QueueJoin(4, 1, token);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            AssertRejected(arena, occupied);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SecretExpiresWhenPrimaryDisconnectsOrIsDestroyed(bool destroyPrimary)
        {
            using var arena = new LobbyTestArena();
            Entity primary = arena.QueueJoin(1, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            LocalPlayerGroup oldGroup = arena.Manager.GetComponentData<LocalPlayerGroup>(primary);
            if (destroyPrimary)
            {
                arena.Manager.DestroyEntity(primary);
            }
            else
            {
                arena.Manager.AddComponent<NetworkStreamRequestDisconnect>(primary);
            }

            Entity rejected = arena.QueueJoin(2, 1, oldGroup.JoinToken);
            Entity newPrimary = arena.QueueJoin(destroyPrimary ? 1 : 3, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            AssertRejected(arena, rejected);
            Assert.That(arena.Manager.GetComponentData<LocalPlayerGroup>(newPrimary).Id, Is.Not.EqualTo(oldGroup.Id));
        }

        [Test]
        public void SecretFromPreviousServerSessionCannotJoinNewGroupWithSamePublicId()
        {
            using var previousSession = new LobbyTestArena();
            Entity previousPrimary = previousSession.QueueJoin(1, 0);
            previousSession.JoinSystem.Update(previousSession.World.Unmanaged);
            LocalPlayerGroup previousGroup = previousSession.Manager.GetComponentData<LocalPlayerGroup>(previousPrimary);
            using var nextSession = new LobbyTestArena();
            Entity nextPrimary = nextSession.QueueJoin(1, 0);
            nextSession.JoinSystem.Update(nextSession.World.Unmanaged);
            Assert.That(nextSession.Manager.GetComponentData<LocalPlayerGroup>(nextPrimary).Id, Is.EqualTo(previousGroup.Id));
            Entity rejected = nextSession.QueueJoin(2, 1, previousGroup.JoinToken);
            nextSession.JoinSystem.Update(nextSession.World.Unmanaged);
            AssertRejected(nextSession, rejected);
        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        public void SecretCannotReplacePrimaryOrChangeItsSharedViewPolicy(int localIndex, bool sharedView)
        {
            using var arena = new LobbyTestArena();
            Entity primary = arena.QueueJoin(1, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            FixedString64Bytes token = arena.Manager.GetComponentData<LocalPlayerGroup>(primary).JoinToken;
            Entity rejected = arena.QueueJoin(2, localIndex, token, sharedView);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            AssertRejected(arena, rejected);
        }

        private static void AssertRejected(LobbyTestArena arena, Entity connection)
        {
            Assert.That(arena.Manager.HasComponent<NetworkStreamRequestDisconnect>(connection), Is.True);
            Assert.That(arena.Manager.HasComponent<NetworkStreamInGame>(connection), Is.False);
            Assert.That(arena.Manager.HasComponent<LobbyMember>(connection), Is.False);
            Assert.That(arena.Manager.GetBuffer<LinkedEntityGroup>(connection).Length, Is.EqualTo(1));
        }
    }
}
