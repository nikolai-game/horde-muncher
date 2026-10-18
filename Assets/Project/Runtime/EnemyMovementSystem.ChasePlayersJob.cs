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
        private partial struct ChasePlayersJob : IJobEntity
        {
            [ReadOnly]
            public NativeArray<LocalTransform> PlayerTransforms;
            [ReadOnly]
            public NativeArray<CombatHealth> PlayerHealth;
            [ReadOnly]
            public NativeParallelMultiHashMap<int2, EnemyNeighbour> SpatialGrid;
            public float DeltaTime;
            public float CellSize;
            public float2 ArenaCenter;
            public float2 ArenaHalfExtents;

            private void Execute(Entity entity, ref LocalTransform transform, in ArenaEnemy enemy)
            {
                float2 position = transform.Position.xz;
                float2 targetOffset = FindNearestLivingPlayerOffset(position, out float targetDistanceSquared);
                float targetDistance = math.sqrt(targetDistanceSquared);
                float movementStep = math.min(enemy.Speed * DeltaTime, math.max(0, targetDistance - enemy.StopDistance));
                float2 separation = CalculateSeparation(entity, position, enemy);
                separation /= math.max(1, math.length(separation));
                float separationStep = math.min(enemy.SeparationSpeed * DeltaTime, (2 * enemy.Radius + enemy.SeparationGap) * 0.25f);
                position += math.normalizesafe(targetOffset) * movementStep + separation * separationStep;
                float2 movementHalfExtents = math.max(float2.zero, ArenaHalfExtents - enemy.Radius);
                float2 nextPosition = math.clamp(position, ArenaCenter - movementHalfExtents, ArenaCenter + movementHalfExtents);
                float2 movementOffset = nextPosition - transform.Position.xz;
                transform.Position.xz = nextPosition;
                if (math.lengthsq(movementOffset) > 0.00000001f)
                {
                    transform.Rotation = quaternion.LookRotationSafe(new float3(movementOffset.x, 0, movementOffset.y), math.up());
                }
            }

            private float2 FindNearestLivingPlayerOffset(float2 enemyPosition, out float nearestDistanceSquared)
            {
                float2 nearestPlayerOffset = float2.zero;
                nearestDistanceSquared = float.MaxValue;
                for (int playerIndex = 0; playerIndex < PlayerTransforms.Length; playerIndex++)
                {
                    if (PlayerHealth[playerIndex].Current <= 0)
                    {
                        continue;
                    }

                    float2 playerOffset = PlayerTransforms[playerIndex].Position.xz - enemyPosition;
                    float distanceSquared = math.lengthsq(playerOffset);
                    if (distanceSquared >= nearestDistanceSquared)
                    {
                        continue;
                    }

                    nearestDistanceSquared = distanceSquared;
                    nearestPlayerOffset = playerOffset;
                }

                return nearestPlayerOffset;
            }

            private float2 CalculateSeparation(Entity entity, float2 position, in ArenaEnemy enemy)
            {
                float2 separation = float2.zero;
                int2 currentCell = (int2)math.floor(position / CellSize);
                int neighbourChecks = 0;
                for (int cellIndex = 0; cellIndex < 9 && neighbourChecks < MAX_NEIGHBOUR_CHECKS; cellIndex++)
                {
                    int neighbourCellIndex = cellIndex == 0 ? 4 : cellIndex <= 4 ? cellIndex - 1 : cellIndex;
                    int2 neighbourCell = currentCell + new int2(neighbourCellIndex % 3 - 1, neighbourCellIndex / 3 - 1);
                    if (!SpatialGrid.TryGetFirstValue(neighbourCell,
                        out EnemyNeighbour neighbour,
                        out NativeParallelMultiHashMapIterator<int2> iterator))
                    {
                        continue;
                    }

                    do
                    {
                        if (++neighbourChecks > MAX_NEIGHBOUR_CHECKS)
                        {
                            break;
                        }

                        if (neighbour.Entity == entity)
                        {
                            continue;
                        }

                        float2 directionAway = position - neighbour.Position;
                        float distance = math.length(directionAway);
                        float spacing = enemy.Radius + neighbour.Radius + math.max(enemy.SeparationGap, neighbour.SeparationGap);
                        if (distance >= spacing)
                        {
                            continue;
                        }

                        if (distance > 0.0001f)
                        {
                            directionAway /= distance;
                        }
                        else
                        {
                            directionAway = CalculateCoincidentSeparationDirection(entity, neighbour.Entity);
                        }

                        separation += directionAway * (1 - distance / spacing);
                    }
                    while (SpatialGrid.TryGetNextValue(out neighbour, ref iterator));
                }

                return separation;
            }

            private static float2 CalculateCoincidentSeparationDirection(Entity entity, Entity neighbour)
            {
                uint pairHash = math.hash(new uint2((uint)math.min(entity.Index, neighbour.Index), (uint)math.max(entity.Index, neighbour.Index)));
                float angle = (pairHash % 65536) * (2 * math.PI / 65536);
                return new float2(math.cos(angle), math.sin(angle)) * (entity.Index < neighbour.Index ? 1 : -1);
            }
        }
    }
}
