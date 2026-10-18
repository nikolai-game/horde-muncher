using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace HordeMuncher.Tests
{
    public sealed class ArenaPauseTests
    {
        [Test]
        public void RemotePlayerPausesEveryoneAndAnotherPlayerResumes()
        {
            using var world = new World("Shared pause tests");
            EntityManager manager = world.EntityManager;
            Entity run = manager.CreateEntity(typeof(WaveProgress));
            manager.SetComponentData(run, new WaveProgress { Phase = WavePhase.Spawning });
            Entity lobby = manager.CreateEntity(typeof(ArenaLobby));
            manager.SetComponentData(lobby, new ArenaLobby { Started = true });
            Entity firstPlayer = manager.CreateEntity(typeof(ArenaPlayer), typeof(WaveStatus));
            Entity secondPlayer = manager.CreateEntity(typeof(ArenaPlayer), typeof(WaveStatus));
            Entity host = CreateConnection(manager, 1);
            Entity remote = CreateConnection(manager, 2);
            ArenaPauseServerSystem pauseSystem = world.CreateSystemManaged<ArenaPauseServerSystem>();

            SendPause(manager, remote, true);
            pauseSystem.Update();
            Assert.That(manager.GetComponentData<WaveProgress>(run).IsPaused, Is.True);
            Assert.That(manager.GetComponentData<WaveStatus>(firstPlayer).IsPaused, Is.True);
            Assert.That(manager.GetComponentData<WaveStatus>(secondPlayer).IsPaused, Is.True);
            Assert.That(CombatPhaseRules.IsActive(manager), Is.False);

            SendPause(manager, remote, true);
            SendPause(manager, host, true);
            pauseSystem.Update();
            Assert.That(manager.GetComponentData<WaveProgress>(run).IsPaused, Is.True);

            SendPause(manager, host, false);
            pauseSystem.Update();
            Assert.That(manager.GetComponentData<WaveProgress>(run).IsPaused, Is.False);
            Assert.That(manager.GetComponentData<WaveStatus>(secondPlayer).IsPaused, Is.False);
            Assert.That(CombatPhaseRules.IsActive(manager), Is.True);
            using EntityQuery requests = manager.CreateEntityQuery(typeof(SetPauseRpc));
            Assert.That(requests.IsEmptyIgnoreFilter, Is.True);
        }

        [Test]
        public void DisconnectedPlayerCannotPauseAndLobbyIgnoresPause()
        {
            using var world = new World("Invalid pause tests");
            EntityManager manager = world.EntityManager;
            Entity run = manager.CreateEntity(typeof(WaveProgress));
            manager.SetComponentData(run, new WaveProgress { Phase = WavePhase.Spawning });
            Entity lobby = manager.CreateEntity(typeof(ArenaLobby));
            Entity connection = CreateConnection(manager, 2);
            ArenaPauseServerSystem system = world.CreateSystemManaged<ArenaPauseServerSystem>();
            SendPause(manager, connection, true);
            system.Update();
            Assert.That(manager.GetComponentData<WaveProgress>(run).IsPaused, Is.False);

            manager.SetComponentData(lobby, new ArenaLobby { Started = true });
            manager.AddComponent<NetworkStreamRequestDisconnect>(connection);
            SendPause(manager, connection, true);
            system.Update();
            Assert.That(manager.GetComponentData<WaveProgress>(run).IsPaused, Is.False);
        }

        [Test]
        public void PausedWavePreservesTimersAndPendingSpawns()
        {
            using var world = new World("Paused wave tests");
            EntityManager manager = world.EntityManager;
            Entity run = manager.CreateEntity(typeof(WaveProgress), typeof(WaveRuntimeSettings));
            manager.SetComponentData(run, new WaveProgress
            {
                IsPaused = true, Phase = WavePhase.Spawning, Elapsed = 5, Budget = 2
            });
            Entity pending = manager.CreateEntity(typeof(PendingEnemySpawn));
            manager.SetComponentData(pending, new PendingEnemySpawn { Remaining = 1 });
            world.SetTime(new TimeData(10, 10));
            world.CreateSystemManaged<RunLifecycleSystem>().Update();
            world.CreateSystemManaged<WaveSpawnSystem>().Update();
            world.CreateSystemManaged<WaveCompletionSystem>().Update();
            WaveProgress progress = manager.GetComponentData<WaveProgress>(run);
            Assert.That(progress.Elapsed, Is.EqualTo(5));
            Assert.That(progress.Budget, Is.EqualTo(2));
            Assert.That(progress.Phase, Is.EqualTo(WavePhase.Spawning));
            Assert.That(manager.GetComponentData<PendingEnemySpawn>(pending).Remaining, Is.EqualTo(1));
        }

        [Test]
        public void PausedPlayerDoesNotMoveAndResumesWithSameInput()
        {
            using var world = new World("Paused player tests");
            EntityManager manager = world.EntityManager;
            Entity player = manager.CreateEntity(typeof(ArenaPlayer), typeof(Simulate), typeof(LocalTransform), typeof(PlayerInput),
                typeof(PlayerMovement), typeof(CombatHealth), typeof(PlayerBonuses), typeof(WaveStatus));
            manager.SetComponentData(player, LocalTransform.Identity);
            manager.SetComponentData(player, new CombatHealth { Current = 100 });
            manager.SetComponentData(player, new PlayerMovement { Speed = 5, HalfExtent = 20 });
            manager.SetComponentData(player, new PlayerInput { Move = new float2(1, 0) });
            manager.SetComponentData(player, new WaveStatus { Phase = WavePhase.Spawning, IsPaused = true });
            SimulationSystemGroup group = world.CreateSystemManaged<SimulationSystemGroup>();
            group.AddSystemToUpdateList(world.GetOrCreateSystem<PlayerMovementSystem>());
            world.SetTime(new TimeData(1, 1));
            group.Update();
            manager.CompleteAllTrackedJobs();
            Assert.That(manager.GetComponentData<LocalTransform>(player).Position.x, Is.Zero);
            manager.SetComponentData(player, new WaveStatus { Phase = WavePhase.Spawning });
            world.SetTime(new TimeData(2, 1));
            group.Update();
            manager.CompleteAllTrackedJobs();
            Assert.That(manager.GetComponentData<LocalTransform>(player).Position.x, Is.EqualTo(5));
        }

        private static Entity CreateConnection(EntityManager manager, int localPlayerGroupId)
        {
            Entity connection = manager.CreateEntity(typeof(LobbyMember), typeof(NetworkStreamInGame));
            manager.SetComponentData(connection, new LobbyMember { LocalPlayerGroupId = localPlayerGroupId });
            return connection;
        }

        private static void SendPause(EntityManager manager, Entity connection, bool isPaused)
        {
            Entity request = manager.CreateEntity(typeof(SetPauseRpc), typeof(ReceiveRpcCommandRequest));
            manager.SetComponentData(request, new SetPauseRpc { IsPaused = isPaused });
            manager.SetComponentData(request, new ReceiveRpcCommandRequest { SourceConnection = connection });
        }
    }
}
