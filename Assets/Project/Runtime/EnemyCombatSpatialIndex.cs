using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HordeMuncher
{
    public struct EnemyCombatSpatialIndex : IDisposable
    {
        public const float CELL_SIZE = 2;

        private NativeParallelMultiHashMap<int2, int> _enemyIndices;
        private NativeArray<Entity> _entities;
        private NativeArray<LocalTransform> _transforms;

        public NativeArray<Entity> Entities => _entities;
        public NativeArray<LocalTransform> Transforms => _transforms;
        public float MaximumRadius { get; private set; }

        public EnemyCombatSpatialIndex(EntityQuery enemyQuery)
        {
            _entities = enemyQuery.ToEntityArray(Allocator.Temp);
            _transforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            _enemyIndices = new NativeParallelMultiHashMap<int2, int>(math.max(1, _entities.Length), Allocator.Temp);
            MaximumRadius = 0;
            using NativeArray<ArenaEnemy> enemies = enemyQuery.ToComponentDataArray<ArenaEnemy>(Allocator.Temp);
            for (int enemyIndex = 0; enemyIndex < _entities.Length; enemyIndex++)
            {
                _enemyIndices.Add(PositionToCell(_transforms[enemyIndex].Position.xz), enemyIndex);
                MaximumRadius = math.max(MaximumRadius, enemies[enemyIndex].Radius);
            }
        }

        public void CollectCandidates(float2 minimumPosition, float2 maximumPosition, ref NativeList<int> candidates)
        {
            candidates.Clear();
            int2 minimumCell = PositionToCell(minimumPosition);
            int2 maximumCell = PositionToCell(maximumPosition);
            float2 cellCounts = (float2)maximumCell - minimumCell + 1;
            if (cellCounts.x * cellCounts.y >= _entities.Length)
            {
                for (int enemyIndex = 0; enemyIndex < _entities.Length; enemyIndex++)
                {
                    candidates.Add(enemyIndex);
                }

                return;
            }

            for (int cellX = minimumCell.x; cellX <= maximumCell.x; cellX++)
            {
                for (int cellY = minimumCell.y; cellY <= maximumCell.y; cellY++)
                {
                    if (!_enemyIndices.TryGetFirstValue(new int2(cellX, cellY), out int enemyIndex, out var iterator))
                    {
                        continue;
                    }

                    do
                    {
                        candidates.Add(enemyIndex);
                    }
                    while (_enemyIndices.TryGetNextValue(out enemyIndex, ref iterator));
                }
            }

            candidates.AsArray().Sort();
        }

        public void UpdateTransform(int enemyIndex, LocalTransform transform)
        {
            int2 oldCell = PositionToCell(_transforms[enemyIndex].Position.xz);
            int2 newCell = PositionToCell(transform.Position.xz);
            if (!math.all(oldCell == newCell))
            {
                _enemyIndices.Remove(oldCell, enemyIndex);
                _enemyIndices.Add(newCell, enemyIndex);
            }

            _transforms[enemyIndex] = transform;
        }

        public void Dispose()
        {
            _enemyIndices.Dispose();
            _transforms.Dispose();
            _entities.Dispose();
        }

        private static int2 PositionToCell(float2 position) => (int2)math.floor(position / CELL_SIZE);
    }
}
