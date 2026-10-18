using Unity.Entities;
using Unity.Mathematics;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WaveSpawnSystem))]
    [UpdateBefore(typeof(EnemyMovementSystem))]
    public partial class WaveCompletionSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<WaveRuntimeSettings>();
        }

        protected override void OnUpdate()
        {
            EntityManager entityManager = EntityManager;
            Entity runEntity = SystemAPI.GetSingletonEntity<WaveRuntimeSettings>();
            WaveProgress waveProgress = entityManager.GetComponentData<WaveProgress>(runEntity);
            if (waveProgress.IsPaused)
            {
                return;
            }

            DynamicBuffer<WavePlan> wavePlans = entityManager.GetBuffer<WavePlan>(runEntity);
            int waveCount = wavePlans.Length;
            float waveDuration = waveProgress.Phase == WavePhase.Spawning ? wavePlans[waveProgress.Index].Duration : 0;
            bool bossDefeated = waveProgress.BossSpawned
                && (!entityManager.Exists(waveProgress.Boss)
                    || entityManager.GetComponentData<CombatHealth>(waveProgress.Boss).Current <= 0);
            bool canComplete = waveProgress.Phase == WavePhase.Spawning
                && (wavePlans[waveProgress.Index].BossPrefab == Entity.Null || bossDefeated);
            if (canComplete && waveProgress.Elapsed >= waveDuration)
            {
                waveProgress.Phase = WavePhase.WaveCleared;
                waveProgress.CompletionElapsed = 0;
                ArenaRunRules.CompleteWave(entityManager);
            }
            else if (waveProgress.Phase == WavePhase.WaveCleared)
            {
                waveProgress.CompletionElapsed += SystemAPI.Time.DeltaTime;
            }

            WaveRuntimeSettings settings = entityManager.GetComponentData<WaveRuntimeSettings>(runEntity);
            if (waveProgress.Phase == WavePhase.WaveCleared
                && waveProgress.CompletionElapsed >= math.max(ArenaRunRules.RESOURCE_COLLECTION_DURATION, settings.CompletionPauseDuration)
                && GetEntityQuery(typeof(ResourcePickup)).IsEmptyIgnoreFilter)
            {
                waveProgress.Phase = waveProgress.Index + 1 >= waveCount ? WavePhase.Complete : WavePhase.Rewards;
                if (waveProgress.Phase == WavePhase.Rewards)
                {
                    ArenaRunRules.ReviveDefeatedPlayers(entityManager);
                }
            }

            entityManager.SetComponentData(runEntity, waveProgress);
            var waveStatus = new WaveStatus
            {
                Number = waveCount == 0 ? 0 : waveProgress.Index + 1,
                Total = waveCount,
                CompletionElapsed = waveProgress.CompletionElapsed,
                Phase = waveProgress.Phase,
                Remaining = waveProgress.Phase == WavePhase.Spawning ? waveDuration - waveProgress.Elapsed : 0
            };
            foreach (RefRW<WaveStatus> playerStatus in SystemAPI.Query<RefRW<WaveStatus>>().WithAll<ArenaPlayer>())
            {
                playerStatus.ValueRW = waveStatus;
            }
        }
    }
}
