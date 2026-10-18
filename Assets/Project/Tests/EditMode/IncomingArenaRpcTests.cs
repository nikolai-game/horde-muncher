using HordeMuncher.Tests.Menu;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher.Tests
{
    public sealed class IncomingArenaRpcTests
    {
        [Test]
        public void MixedRpcFloodDisconnectsOnlyItsSenderAndCannotChangeLobby()
        {
            using var arena = new LobbyTestArena();
            Entity attacker = arena.QueueJoin(1, 0);
            Entity other = arena.QueueJoin(2, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            IncomingArenaRpcSystem filter = arena.World.CreateSystemManaged<IncomingArenaRpcSystem>();
            for (int index = 0; index < IncomingRpcBudget.BURST_CAPACITY + 1; index++)
            {
                if (index % 2 == 0)
                {
                    arena.UpdateSelection(attacker, true);
                }
                else
                {
                    arena.SetReady(attacker, true, joinSelection: false);
                }
            }

            arena.UpdateSelection(other, true);
            filter.Update();
            arena.LobbySystem.Update();
            Assert.That(arena.Manager.HasComponent<NetworkStreamRequestDisconnect>(attacker), Is.True);
            Assert.That(arena.Manager.GetComponentData<LobbyMember>(attacker).SelectionJoined, Is.False);
            Assert.That(arena.Manager.HasComponent<NetworkStreamRequestDisconnect>(other), Is.False);
            Assert.That(arena.Manager.GetComponentData<LobbyMember>(other).SelectionJoined, Is.True);
        }

        [Test]
        public void BudgetRefillsOverTimeAndInvalidDirectionAlsoCostsTokens()
        {
            using var arena = new LobbyTestArena();
            Entity connection = arena.QueueJoin(1, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            IncomingArenaRpcSystem filter = arena.World.CreateSystemManaged<IncomingArenaRpcSystem>();
            for (int index = 0; index < IncomingRpcBudget.BURST_CAPACITY; index++)
            {
                Entity invalid = arena.Manager.CreateEntity(typeof(LobbySnapshotRpc), typeof(ReceiveRpcCommandRequest));
                arena.Manager.SetComponentData(invalid, new ReceiveRpcCommandRequest { SourceConnection = connection });
            }

            filter.Update();
            using EntityQuery requests = arena.Manager.CreateEntityQuery(typeof(ReceiveRpcCommandRequest));
            Assert.That(requests.IsEmptyIgnoreFilter, Is.True);
            Assert.That(arena.Manager.GetComponentData<IncomingRpcBudget>(connection).Tokens, Is.Zero);
            arena.World.SetTime(new TimeData(1, 1));
            arena.UpdateSelection(connection, true);
            filter.Update();
            arena.LobbySystem.Update();
            Assert.That(arena.Manager.GetComponentData<IncomingRpcBudget>(connection).Tokens, Is.EqualTo(15));
            Assert.That(arena.Manager.GetComponentData<LobbyMember>(connection).SelectionJoined, Is.True);
        }

        [Test]
        public void UnjoinedConnectionAndStartedLobbyCannotChangeSelection()
        {
            using var arena = new LobbyTestArena();
            Entity joined = arena.QueueJoin(1, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            Entity unjoined = arena.Manager.CreateEntity(typeof(NetworkId));
            arena.Manager.SetComponentData(unjoined, new NetworkId { Value = 99 });
            arena.UpdateSelection(unjoined, true);
            IncomingArenaRpcSystem filter = arena.World.CreateSystemManaged<IncomingArenaRpcSystem>();
            filter.Update();
            using (EntityQuery pending = arena.Manager.CreateEntityQuery(typeof(ReceiveRpcCommandRequest)))
            {
                Assert.That(pending.IsEmptyIgnoreFilter, Is.True);
            }

            arena.Manager.SetComponentData(arena.Lobby, new ArenaLobby { Started = true });
            arena.UpdateSelection(joined, true);
            filter.Update();
            arena.LobbySystem.Update();
            Assert.That(arena.Manager.GetComponentData<LobbyMember>(joined).SelectionJoined, Is.False);
            Assert.That(arena.Manager.HasComponent<LobbyMember>(unjoined), Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void IncompatibleClientGetsReasonAndNeverCreatesPlayer(bool changeProtocol)
        {
            using var arena = new LobbyTestArena();
            Entity connection = arena.QueueJoin(1, 0);
            using (EntityQuery requests = arena.Manager.CreateEntityQuery(typeof(JoinArenaRequest)))
            {
                JoinArenaRequest join = requests.GetSingleton<JoinArenaRequest>();
                if (changeProtocol)
                {
                    join.ProtocolVersion++;
                }
                else
                {
                    join.ContentVersion++;
                }

                arena.Manager.SetComponentData(requests.GetSingletonEntity(), join);
            }

            arena.JoinSystem.Update(arena.World.Unmanaged);
            using EntityQuery players = arena.Manager.CreateEntityQuery(typeof(ArenaPlayer));
            using EntityQuery replies = arena.Manager.CreateEntityQuery(typeof(JoinArenaRejectedRpc), typeof(SendRpcCommandRequest));
            Assert.That(players.IsEmptyIgnoreFilter, Is.True);
            Assert.That(arena.Manager.HasComponent<LobbyMember>(connection), Is.False);
            Assert.That(replies.GetSingleton<JoinArenaRejectedRpc>().Reason.ToString(),
                Does.Contain(changeProtocol ? "протокола" : "контента"));
            Assert.That(replies.GetSingleton<SendRpcCommandRequest>().TargetConnection, Is.EqualTo(connection));
            arena.World.SetTime(new TimeData(4, 4));
            arena.World.CreateSystemManaged<IncomingArenaRpcSystem>().Update();
            Assert.That(arena.Manager.HasComponent<NetworkStreamRequestDisconnect>(connection), Is.True,
                "A client that ignores rejection must still be disconnected");
        }
    }
}
