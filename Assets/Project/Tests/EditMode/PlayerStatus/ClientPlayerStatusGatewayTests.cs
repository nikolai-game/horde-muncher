using HordeMuncher.Client.PlayerStatus;
using HordeMuncher.Tests.Shop;
using HordeMuncher.UI.Contracts;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher.Tests.PlayerStatus
{
    public sealed class ClientPlayerStatusGatewayTests
    {
        [Test]
        public void HudSnapshotUsesOnlyOwnedPlayerAndRemainsIndependentOfEcsChanges()
        {
            using var arena = new ShopBridgeTestArena();
            arena.CreatePlayer(2, false);
            using var gateway = new ClientPlayerStatusGateway(arena.World);
            Assert.That(gateway.TryRead(out HudState state), Is.True);
            Assert.That(state.PlayerNumber, Is.EqualTo(1));
            Assert.That(state.Health, Is.EqualTo(100));
            Assert.That(state.Resources, Is.EqualTo(100));
            Assert.That(state.Phase, Is.EqualTo(RunPhase.Shop));
            arena.World.EntityManager.SetComponentData(arena.Player, new CombatHealth
            {
                Current = 20,
                Maximum = 150
            });
            Assert.That(state.Health, Is.EqualTo(100));
            Assert.That(gateway.TryRead(out HudState changed), Is.True);
            Assert.That(changed.Health, Is.EqualTo(20));
            Assert.That(changed.MaximumHealth, Is.EqualTo(150));
        }

        [TestCase(WavePhase.Complete, RunPhase.Victory)]
        [TestCase(WavePhase.Defeat, RunPhase.Defeat)]
        [TestCase(WavePhase.Spawning, RunPhase.Combat)]
        [TestCase(WavePhase.Waiting, RunPhase.Waiting)]
        [TestCase(WavePhase.WaveCleared, RunPhase.WaveCleared)]
        public void ResultIncludesRemotePlayersInReadinessCount(WavePhase phase, RunPhase expectedPhase)
        {
            using var arena = new ShopBridgeTestArena();
            Entity remote = arena.CreatePlayer(2, false);
            EntityManager entityManager = arena.World.EntityManager;
            PlayerProgress progress = entityManager.GetComponentData<PlayerProgress>(remote);
            progress.RestartReady = true;
            entityManager.SetComponentData(remote, progress);
            entityManager.SetComponentData(arena.Player, new WaveStatus
            {
                Phase = phase
            });
            using var gateway = new ClientPlayerStatusGateway(arena.World);
            Assert.That(gateway.TryRead(out RunResultState state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(expectedPhase));
            Assert.That(state.TotalPlayers, Is.EqualTo(2));
            Assert.That(state.ReadyPlayers, Is.EqualTo(1));
            Assert.That(state.RestartReady, Is.False);
        }

        [Test]
        public void RestartRpcTargetsOwnConnectionAndDoesNotPredictReadiness()
        {
            using var first = new ShopBridgeTestArena(1);
            using var second = new ShopBridgeTestArena(2);
            second.World.EntityManager.SetComponentData(second.Player, new WaveStatus
            {
                Phase = WavePhase.Defeat
            });
            using var gateway = new ClientPlayerStatusGateway(second.World);
            Assert.That(gateway.TryToggleRestartReadiness(5), Is.True);
            Assert.That(first.CountRequests(), Is.Zero);
            Assert.That(second.CountRequests(), Is.EqualTo(1));
            using EntityQuery outgoing = second.World.EntityManager.CreateEntityQuery(typeof(ShopRequest), typeof(SendRpcCommandRequest));
            ShopRequest payload = outgoing.GetSingleton<ShopRequest>();
            Assert.That(payload.Action, Is.EqualTo(ShopAction.Restart));
            Assert.That(payload.Revision, Is.EqualTo(5));
            Assert.That(outgoing.GetSingleton<SendRpcCommandRequest>().TargetConnection, Is.EqualTo(second.Connection));
            Assert.That(second.World.EntityManager.GetComponentData<PlayerProgress>(second.Player).RestartReady, Is.False);
        }

        [Test]
        public void StaleRevisionWrongPhaseAndWrongOwnershipDoNotCreateRestartRpc()
        {
            using var arena = new ShopBridgeTestArena();
            using var gateway = new ClientPlayerStatusGateway(arena.World);
            Assert.That(gateway.TryToggleRestartReadiness(5), Is.False);
            arena.World.EntityManager.SetComponentData(arena.Player, new WaveStatus
            {
                Phase = WavePhase.Complete
            });
            Assert.That(gateway.TryToggleRestartReadiness(4), Is.False);
            arena.World.EntityManager.SetComponentData(arena.Player, new GhostOwner
            {
                NetworkId = 2
            });
            Assert.That(gateway.TryRead(out HudState _), Is.False);
            Assert.That(gateway.TryToggleRestartReadiness(5), Is.False);
            Assert.That(arena.CountRequests(), Is.Zero);
        }

        [Test]
        public void AmbiguousPlayersAndConnectionsAndDisconnectAreUnavailable()
        {
            using var arena = new ShopBridgeTestArena();
            using var gateway = new ClientPlayerStatusGateway(arena.World);
            EntityManager entityManager = arena.World.EntityManager;
            Entity duplicate = arena.CreatePlayer(1, true);
            Assert.That(gateway.TryRead(out HudState _), Is.False);
            entityManager.DestroyEntity(duplicate);
            Entity connection = entityManager.CreateEntity(typeof(NetworkId), typeof(NetworkStreamInGame));
            Assert.That(gateway.TryRead(out RunResultState _), Is.False);
            entityManager.DestroyEntity(connection);
            entityManager.AddComponent<NetworkStreamRequestDisconnect>(arena.Connection);
            Assert.That(gateway.TryRead(out HudState _), Is.False);
            Assert.That(gateway.TryRead(out RunResultState _), Is.False);
            Assert.That(gateway.TryToggleRestartReadiness(5), Is.False);
        }

        [Test]
        public void DespawnDisposeAndWorldShutdownAreSafe()
        {
            using var arena = new ShopBridgeTestArena();
            using var gateway = new ClientPlayerStatusGateway(arena.World);
            arena.World.EntityManager.DestroyEntity(arena.Player);
            Assert.That(gateway.TryRead(out HudState _), Is.False);
            gateway.Dispose();
            Assert.That(gateway.TryRead(out RunResultState _), Is.False);
            arena.World.Dispose();
            Assert.That(gateway.TryRead(out HudState _), Is.False);
            Assert.That(gateway.TryToggleRestartReadiness(5), Is.False);
        }

        [Test]
        public void RestartPayloadIsAcceptedOnceByServerAndReadinessCanThenBeCancelled()
        {
            using var client = new ShopBridgeTestArena();
            client.World.EntityManager.SetComponentData(client.Player, new WaveStatus
            {
                Phase = WavePhase.Complete
            });
            using var gateway = new ClientPlayerStatusGateway(client.World);
            Assert.That(gateway.TryToggleRestartReadiness(5), Is.True);
            using EntityQuery outgoing = client.World.EntityManager.CreateEntityQuery(typeof(ShopRequest));
            ShopRequest payload = outgoing.GetSingleton<ShopRequest>();
            using var server = new ShopBridgeTestArena(isServer: true);
            EntityManager entityManager = server.World.EntityManager;
            entityManager.AddComponentData(server.Config, new WaveProgress
            {
                Phase = WavePhase.Complete
            });
            for (int requestIndex = 0; requestIndex < 2; requestIndex++)
            {
                ReceiveRequest(entityManager, server.Connection, payload);
            }

            ShopSystem shop = server.World.CreateSystemManaged<ShopSystem>();
            server.World.SetTime(new TimeData(0.1, 0.1f));
            shop.Update();
            PlayerProgress progress = entityManager.GetComponentData<PlayerProgress>(server.Player);
            Assert.That(progress.RestartReady, Is.True);
            Assert.That(progress.Revision, Is.EqualTo(6));
            payload.Revision = progress.Revision;
            ReceiveRequest(entityManager, server.Connection, payload);
            shop.Update();
            Assert.That(entityManager.GetComponentData<PlayerProgress>(server.Player).RestartReady, Is.False);
        }

        private static void ReceiveRequest(EntityManager entityManager, Entity connection, ShopRequest payload)
        {
            Entity received = entityManager.CreateEntity(typeof(ShopRequest), typeof(ReceiveRpcCommandRequest));
            entityManager.SetComponentData(received, payload);
            entityManager.SetComponentData(received, new ReceiveRpcCommandRequest
            {
                SourceConnection = connection
            });
        }
    }
}
