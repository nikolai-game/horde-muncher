using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HordeMuncher
{
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EnemyMovementSystem : ISystem
    {
        private const int MAX_NEIGHBOUR_CHECKS = 128;
        private EntityQuery _playerQuery;
        private EntityQuery _enemyQuery;

        public void OnCreate(ref SystemState state)
        {
            _playerQuery = SystemAPI.QueryBuilder().WithAll<ArenaPlayer, LocalTransform, CombatHealth>().Build();
            _enemyQuery = SystemAPI.QueryBuilder().WithAll<ArenaEnemy, LocalTransform>().Build();
            state.RequireForUpdate(_playerQuery);
            state.RequireForUpdate<ArenaEnemy>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton(out WaveProgress waveProgress) && (waveProgress.IsPaused || waveProgress.Phase != WavePhase.Spawning))
            {
                return;
            }

            NativeArray<LocalTransform> playerTransforms = _playerQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            NativeArray<CombatHealth> playerHealth = _playerQuery.ToComponentDataArray<CombatHealth>(Allocator.TempJob);
            var spatialGrid = new NativeParallelMultiHashMap<int2, EnemyNeighbour>(_enemyQuery.CalculateEntityCount(), Allocator.TempJob);
            float cellSize = 0.1f;
            using (NativeArray<ArenaEnemy> enemies = _enemyQuery.ToComponentDataArray<ArenaEnemy>(Allocator.Temp))
            {
                foreach (ArenaEnemy enemy in enemies)
                {
                    cellSize = math.max(cellSize, 2 * enemy.Radius + enemy.SeparationGap);
                }
            }

            float2 arenaCenter = float2.zero;
            var arenaHalfExtents = new float2(18.825f);
            if (SystemAPI.TryGetSingleton(out WaveRuntimeSettings arenaSettings))
            {
                arenaCenter = arenaSettings.Center;
                arenaHalfExtents = arenaSettings.HalfExtents;
            }

            state.Dependency = new BuildEnemySpatialGridJob
            {
                SpatialGrid = spatialGrid.AsParallelWriter(),
                CellSize = cellSize
            }.ScheduleParallel(state.Dependency);
            state.Dependency = new ChasePlayersJob
            {
                PlayerTransforms = playerTransforms,
                PlayerHealth = playerHealth,
                SpatialGrid = spatialGrid,
                CellSize = cellSize,
                ArenaCenter = arenaCenter,
                ArenaHalfExtents = arenaHalfExtents,
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(state.Dependency);
            state.Dependency = playerTransforms.Dispose(state.Dependency);
            state.Dependency = playerHealth.Dispose(state.Dependency);
            state.Dependency = spatialGrid.Dispose(state.Dependency);
        }
    }
}
