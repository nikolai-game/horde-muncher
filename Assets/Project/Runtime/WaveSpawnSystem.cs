using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RunLifecycleSystem))]
    [UpdateBefore(typeof(WaveCompletionSystem))]
    public partial class WaveSpawnSystem : SystemBase
    {
        private const int FULL_CIRCLE_PI_FACTOR = 2;
        private const int DIAMETER_FACTOR = 2;
        private const int BOSS_ADD_MAX_EXTRA_DISTANCE = 4;

        private const float SPAWN_WARNING_DURATION = 1.5f;
        private const float PLAYER_SPAWN_CLEARANCE = 0.75f;
        private const float MAXIMUM_SPEED_GROWTH = 0.2f;
        private const float SPAWN_RING_WIDTH = 3;

        private EntityQuery _pendingSpawnQuery;
        private int _nextWarningId;
        private EntityQuery _playerQuery;
        private EntityQuery _enemyQuery;

        protected override void OnCreate()
        {
            _pendingSpawnQuery = GetEntityQuery(typeof(PendingEnemySpawn));
            _playerQuery = GetEntityQuery(typeof(ArenaPlayer), typeof(CombatHealth), typeof(LocalTransform));
            _enemyQuery = GetEntityQuery(typeof(ArenaEnemy));
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

            if (waveProgress.Phase != WavePhase.Spawning)
            {
                CancelPendingSpawns();
                return;
            }

            WaveRuntimeSettings waveSettings = entityManager.GetComponentData<WaveRuntimeSettings>(runEntity);
            DynamicBuffer<WavePlan> wavePlans = entityManager.GetBuffer<WavePlan>(runEntity);
            DynamicBuffer<WaveSpawnRateCurvePoint> spawnRatePoints = entityManager.GetBuffer<WaveSpawnRateCurvePoint>(runEntity);
            using NativeArray<WaveEnemy> enemyChoices = entityManager.GetBuffer<WaveEnemy>(runEntity).ToNativeArray(Allocator.Temp);
            WavePlan wavePlan = wavePlans[waveProgress.Index];
            float nextElapsedTime = math.min(wavePlan.Duration, waveProgress.Elapsed + SystemAPI.Time.DeltaTime);
            using NativeArray<CombatHealth> playerHealth = _playerQuery.ToComponentDataArray<CombatHealth>(Allocator.Temp);
            int livingPlayerCount = 0;
            foreach (CombatHealth health in playerHealth)
            {
                if (health.Current > 0)
                {
                    livingPlayerCount++;
                }
            }

            int spawnPlayerCount = math.max(1, livingPlayerCount);
            waveProgress.Budget += WaveMath.CalculateSpawnAmount(spawnRatePoints, wavePlan, waveProgress.Elapsed, nextElapsedTime)
                * spawnPlayerCount;
            int scheduledSpawnCount = (int)math.floor(waveProgress.Budget);
            waveProgress.Budget -= scheduledSpawnCount;
            int remainingEnemyCapacity = math.max(0,
                waveSettings.MaxAlive * spawnPlayerCount - _enemyQuery.CalculateEntityCount() - _pendingSpawnQuery.CalculateEntityCount());
            int spawnCount = math.min(scheduledSpawnCount, math.min(waveSettings.MaxSpawnsPerTick * spawnPlayerCount, remainingEnemyCapacity));
            var random = new Random(waveProgress.RandomState);
            using NativeArray<LocalTransform> playerTransforms = _playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            ProcessPendingSpawns(waveSettings, playerTransforms, playerHealth, ref random, ref waveProgress);
            for (int spawnIndex = 0; spawnIndex < spawnCount && wavePlan.EnemyCount > 0; spawnIndex++)
            {
                Entity enemyPrefab = enemyChoices[wavePlan.EnemyOffset + random.NextInt(wavePlan.EnemyCount)].Prefab;
                if (!entityManager.HasComponent<ArenaEnemy>(enemyPrefab))
                {
                    continue;
                }

                ArenaEnemy enemy = entityManager.GetComponentData<ArenaEnemy>(enemyPrefab);
                float2 spawnHalfExtents = waveSettings.HalfExtents - enemy.Radius;
                if (math.any(spawnHalfExtents <= 0))
                {
                    continue;
                }

                if (!TryFindSpawnPosition(waveSettings,
                    enemy,
                    spawnHalfExtents,
                    playerTransforms,
                    playerHealth,
                    ref random,
                    out float2 spawnPosition))
                {
                    continue;
                }

                ScheduleSpawn(enemyPrefab, spawnPosition, enemy.Radius, false);
            }

            if (nextElapsedTime >= wavePlan.Duration && wavePlan.BossPrefab != Entity.Null && !waveProgress.BossSpawned && !HasPendingBoss())
            {
                ArenaEnemy boss = entityManager.GetComponentData<ArenaEnemy>(wavePlan.BossPrefab);
                float2 bossSpawnHalfExtents = waveSettings.HalfExtents - boss.Radius;
                if (math.all(bossSpawnHalfExtents > 0)
                    && TryFindSpawnPosition(waveSettings,
                        boss,
                        bossSpawnHalfExtents,
                        playerTransforms,
                        playerHealth,
                        ref random,
                        out float2 bossSpawnPosition))
                {
                    ScheduleSpawn(wavePlan.BossPrefab, bossSpawnPosition, boss.Radius, true);
                }
            }

            waveProgress.RandomState = random.state;
            waveProgress.Elapsed = nextElapsedTime;
            entityManager.SetComponentData(runEntity, waveProgress);
        }

        private void ScheduleSpawn(Entity prefab, float2 position, float radius, bool isBoss)
        {
            var pendingSpawn = new PendingEnemySpawn
            {
                Prefab = prefab,
                Position = position,
                Remaining = SPAWN_WARNING_DURATION,
                WarningId = ++_nextWarningId,
                IsBoss = isBoss
            };
            EntityManager.AddComponentData(EntityManager.CreateEntity(), pendingSpawn);
            SendWarning(pendingSpawn, radius, false);
        }

        private void SendWarning(PendingEnemySpawn pendingSpawn, float radius, bool removed)
        {
            Entity warning = EntityManager.CreateEntity(typeof(EnemySpawnWarningRpc), typeof(SendRpcCommandRequest));
            EntityManager.SetComponentData(warning, new EnemySpawnWarningRpc
            {
                Id = pendingSpawn.WarningId,
                Position = pendingSpawn.Position,
                Radius = radius,
                Removed = removed
            });
        }

        private void CancelPendingSpawns()
        {
            using NativeArray<PendingEnemySpawn> pendingSpawns =
                _pendingSpawnQuery.ToComponentDataArray<PendingEnemySpawn>(Allocator.Temp);
            foreach (PendingEnemySpawn pendingSpawn in pendingSpawns)
            {
                SendWarning(pendingSpawn, 0, true);
            }

            EntityManager.DestroyEntity(_pendingSpawnQuery);
        }

        private bool HasPendingBoss()
        {
            using NativeArray<PendingEnemySpawn> pendingSpawns =
                _pendingSpawnQuery.ToComponentDataArray<PendingEnemySpawn>(Allocator.Temp);
            foreach (PendingEnemySpawn pendingSpawn in pendingSpawns)
            {
                if (pendingSpawn.IsBoss)
                {
                    return true;
                }
            }

            return false;
        }

        private void ProcessPendingSpawns(WaveRuntimeSettings settings,
            NativeArray<LocalTransform> playerTransforms,
            NativeArray<CombatHealth> playerHealth,
            ref Random random,
            ref WaveProgress progress)
        {
            using NativeArray<Entity> pendingEntities = _pendingSpawnQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity pendingEntity in pendingEntities)
            {
                PendingEnemySpawn pending = EntityManager.GetComponentData<PendingEnemySpawn>(pendingEntity);
                pending.Remaining -= SystemAPI.Time.DeltaTime;
                if (pending.Remaining > 0)
                {
                    EntityManager.SetComponentData(pendingEntity, pending);
                    continue;
                }

                ArenaEnemy enemy = EntityManager.GetComponentData<ArenaEnemy>(pending.Prefab);
                if (!IsPositionClear(pending.Position, enemy.Radius + PLAYER_SPAWN_CLEARANCE, playerTransforms, playerHealth))
                {
                    float2 halfExtents = settings.HalfExtents - enemy.Radius;
                    for (int attempt = 0; attempt < settings.PositionAttempts; attempt++)
                    {
                        float angle = random.NextFloat(0, math.PI * FULL_CIRCLE_PI_FACTOR);
                        float distance = random.NextFloat(enemy.Radius * DIAMETER_FACTOR + 1, enemy.Radius * DIAMETER_FACTOR
                            + BOSS_ADD_MAX_EXTRA_DISTANCE);
                        float2 candidate = math.clamp(pending.Position + new float2(math.cos(angle), math.sin(angle)) * distance,
                            settings.Center - halfExtents, settings.Center + halfExtents);
                        if (IsPositionClear(candidate, enemy.Radius + PLAYER_SPAWN_CLEARANCE, playerTransforms, playerHealth))
                        {
                            pending.Position = candidate;
                            break;
                        }
                    }

                    pending.Remaining = SPAWN_WARNING_DURATION;
                    EntityManager.SetComponentData(pendingEntity, pending);
                    SendWarning(pending, enemy.Radius, false);
                    continue;
                }

                LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(pending.Prefab);
                transform.Position = new float3(pending.Position.x, enemy.SpawnHeight, pending.Position.y);
                Entity spawnedEnemy = EntityManager.Instantiate(pending.Prefab);
                EntityManager.SetComponentData(spawnedEnemy, transform);
                ScaleEnemyStats(spawnedEnemy, settings, progress.Index);
                if (pending.IsBoss)
                {
                    progress.Boss = spawnedEnemy;
                    progress.BossSpawned = true;
                }

                SendWarning(pending, enemy.Radius, true);
                EntityManager.DestroyEntity(pendingEntity);
            }
        }

        private void ScaleEnemyStats(Entity spawnedEnemy, WaveRuntimeSettings settings, int waveIndex)
        {
            int completedWaveCount = math.max(0, waveIndex);
            CombatHealth health = EntityManager.GetComponentData<CombatHealth>(spawnedEnemy);
            health.Maximum = math.max(1, (int)math.round(health.Maximum * (1 + settings.EnemyHealthGrowthPerWave * completedWaveCount)));
            health.Current = health.Maximum;
            EntityManager.SetComponentData(spawnedEnemy, health);

            ArenaEnemy enemy = EntityManager.GetComponentData<ArenaEnemy>(spawnedEnemy);
            enemy.ContactDamage = (int)math.round(enemy.ContactDamage * (1 + settings.EnemyDamageGrowthPerWave * completedWaveCount));
            enemy.Speed *= 1 + math.min(MAXIMUM_SPEED_GROWTH, settings.EnemySpeedGrowthPerWave * completedWaveCount);
            EntityManager.SetComponentData(spawnedEnemy, enemy);
        }

        private static bool IsPositionClear(float2 position,
            float clearance,
            NativeArray<LocalTransform> playerTransforms,
            NativeArray<CombatHealth> playerHealth)
        {
            for (int playerIndex = 0; playerIndex < playerTransforms.Length; playerIndex++)
            {
                if (playerHealth[playerIndex].Current > 0
                    && math.distancesq(position, playerTransforms[playerIndex].Position.xz) < clearance * clearance)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryFindSpawnPosition(WaveRuntimeSettings waveSettings,
            ArenaEnemy enemy,
            float2 spawnHalfExtents,
            NativeArray<LocalTransform> playerTransforms,
            NativeArray<CombatHealth> playerHealth,
            ref Random random,
            out float2 spawnPosition)
        {
            spawnPosition = default;
            int livingPlayerCount = 0;
            for (int playerIndex = 0; playerIndex < playerHealth.Length; playerIndex++)
            {
                if (playerHealth[playerIndex].Current > 0)
                {
                    livingPlayerCount++;
                }
            }

            if (livingPlayerCount == 0)
            {
                return false;
            }

            int targetPlayerIndex = random.NextInt(livingPlayerCount);
            float2 targetPosition = default;
            for (int playerIndex = 0; playerIndex < playerHealth.Length; playerIndex++)
            {
                if (playerHealth[playerIndex].Current <= 0)
                {
                    continue;
                }

                if (targetPlayerIndex == 0)
                {
                    targetPosition = playerTransforms[playerIndex].Position.xz;
                    break;
                }

                targetPlayerIndex--;
            }

            float minimumDistance = waveSettings.SafeDistance + enemy.Radius;
            float maximumDistance = minimumDistance + SPAWN_RING_WIDTH;
            for (int attempt = 0; attempt < waveSettings.PositionAttempts; attempt++)
            {
                float angle = random.NextFloat(0, math.PI * FULL_CIRCLE_PI_FACTOR);
                float distance = math.sqrt(random.NextFloat(minimumDistance * minimumDistance, maximumDistance * maximumDistance));
                spawnPosition = targetPosition + new float2(math.cos(angle), math.sin(angle)) * distance;
                if (math.any(math.abs(spawnPosition - waveSettings.Center) > spawnHalfExtents))
                {
                    continue;
                }

                if (IsPositionClear(spawnPosition, minimumDistance, playerTransforms, playerHealth))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
