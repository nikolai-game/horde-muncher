using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ShopSystem))]
    [UpdateBefore(typeof(WaveSpawnSystem))]
    public partial class RunLifecycleSystem : SystemBase
    {
        private EntityQuery _playerQuery;

        protected override void OnCreate()
        {
            _playerQuery = GetEntityQuery(typeof(ArenaPlayer), typeof(CombatHealth), typeof(LocalTransform));
            RequireForUpdate<WaveRuntimeSettings>();
        }

        protected override void OnUpdate()
        {
            if (SystemAPI.TryGetSingleton(out ArenaLobby lobby) && !lobby.Started)
            {
                return;
            }

            EntityManager entityManager = EntityManager;
            Entity runEntity = SystemAPI.GetSingletonEntity<WaveRuntimeSettings>();
            WaveProgress waveProgress = entityManager.GetComponentData<WaveProgress>(runEntity);
            WavePhase previousPhase = waveProgress.Phase;
            if (waveProgress.IsPaused)
            {
                return;
            }

            if (waveProgress.Phase == WavePhase.Complete || waveProgress.Phase == WavePhase.Defeat)
            {
                if (AreAllPlayersReady(true, waveProgress.Index)
                    && SystemAPI.TryGetSingleton(out ArenaConfiguration arenaConfiguration))
                {
                    ArenaRunRules.RestartRun(entityManager, runEntity, arenaConfiguration);
                }

                return;
            }

            using NativeArray<CombatHealth> playerHealthStates = _playerQuery.ToComponentDataArray<CombatHealth>(Allocator.Temp);
            bool hasSurvivor = false;
            foreach (CombatHealth playerHealth in playerHealthStates)
            {
                hasSurvivor |= playerHealth.Current > 0;
            }

            if (waveProgress.Phase == WavePhase.Waiting && hasSurvivor)
            {
                StartFirstWave(runEntity, ref waveProgress);
            }

            if (waveProgress.Phase != WavePhase.Waiting && waveProgress.Phase != WavePhase.Complete && !hasSurvivor)
            {
                waveProgress.Phase = WavePhase.Defeat;
            }

            if (waveProgress.Phase == WavePhase.Rewards && HaveAllPlayersResolvedRewards(waveProgress.Index))
            {
                waveProgress.Phase = WavePhase.Shop;
            }

            if (waveProgress.Phase == WavePhase.Shop && AreAllPlayersReady(false, waveProgress.Index))
            {
                waveProgress.Index++;
                waveProgress.Elapsed = 0;
                waveProgress.Budget = 0;
                waveProgress.BossSpawned = false;
                waveProgress.Boss = Entity.Null;
                waveProgress.Phase = WavePhase.Spawning;
            }

            if (waveProgress.Phase == WavePhase.Spawning && previousPhase != WavePhase.Spawning)
            {
                foreach (RefRW<CombatHealth> playerHealth in SystemAPI.Query<RefRW<CombatHealth>>().WithAll<ArenaPlayer>())
                {
                    playerHealth.ValueRW.Current = playerHealth.ValueRO.Maximum;
                    playerHealth.ValueRW.HurtCooldown = 0;
                }
            }

            entityManager.SetComponentData(runEntity, waveProgress);
        }

        private void StartFirstWave(Entity runEntity, ref WaveProgress waveProgress)
        {
            DynamicBuffer<WavePlan> wavePlans = EntityManager.GetBuffer<WavePlan>(runEntity);
            WaveRuntimeSettings waveSettings = EntityManager.GetComponentData<WaveRuntimeSettings>(runEntity);
            waveProgress.Phase = wavePlans.Length > 0 ? WavePhase.Spawning : WavePhase.Complete;
            waveProgress.RandomState = waveSettings.Seed == 0 ? (uint)DateTime.UtcNow.Ticks : waveSettings.Seed;
            waveProgress.RandomState = math.max(1u, waveProgress.RandomState);
        }

        private bool HaveAllPlayersResolvedRewards(int waveIndex)
        {
            using NativeArray<Entity> players = _playerQuery.ToEntityArray(Allocator.Temp);
            if (players.Length == 0)
            {
                return false;
            }

            foreach (Entity player in players)
            {
                if (!EntityManager.HasComponent<PlayerProgress>(player))
                {
                    return false;
                }

                PlayerProgress progress = EntityManager.GetComponentData<PlayerProgress>(player);
                if (progress.ShopWave != waveIndex || progress.PendingUpgrades > 0
                    || (EntityManager.HasBuffer<PendingItemReward>(player) && EntityManager.GetBuffer<PendingItemReward>(player).Length > 0))
                {
                    return false;
                }
            }

            return true;
        }

        private bool AreAllPlayersReady(bool isRestartVote, int waveIndex)
        {
            if (_playerQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            using NativeArray<Entity> playerEntities = _playerQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity playerEntity in playerEntities)
            {
                if (!EntityManager.HasComponent<PlayerProgress>(playerEntity))
                {
                    return false;
                }

                PlayerProgress playerProgress = EntityManager.GetComponentData<PlayerProgress>(playerEntity);
                if (isRestartVote
                    ? !playerProgress.RestartReady
                    : !playerProgress.Ready || playerProgress.PendingUpgrades > 0 || playerProgress.ShopWave != waveIndex)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
