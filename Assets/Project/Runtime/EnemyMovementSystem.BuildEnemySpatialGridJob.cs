using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HordeMuncher
{
    public partial struct EnemyMovementSystem
    {
        [BurstCompile]
        private partial struct BuildEnemySpatialGridJob : IJobEntity
        {
            public NativeParallelMultiHashMap<int2, EnemyNeighbour>.ParallelWriter SpatialGrid;
            public float CellSize;

            private void Execute(Entity entity, in LocalTransform transform, in ArenaEnemy enemy)
            {
                float2 position = transform.Position.xz;
                int2 cell = (int2)math.floor(position / CellSize);
                SpatialGrid.Add(cell, new EnemyNeighbour
                {
                    Entity = entity,
                    Position = position,
                    Radius = enemy.Radius,
                    SeparationGap = enemy.SeparationGap
                });
            }
        }
    }
}
