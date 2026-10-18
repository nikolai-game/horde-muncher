using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemyMovementSystem))]
    [UpdateBefore(typeof(EnemyDeathSystem))]
    public partial class ProjectileSystem : SystemBase
    {
        private const float MIN_MOVEMENT_SQUARED = 1e-10f;

        private EntityQuery _projectileQuery;
        private EntityQuery _enemyQuery;

        protected override void OnCreate()
        {
            _projectileQuery = GetEntityQuery(typeof(ArenaProjectile), typeof(LocalTransform));
            _enemyQuery = GetEntityQuery(typeof(ArenaEnemy), typeof(CombatHealth), typeof(LocalTransform));
            RequireForUpdate<ArenaConfiguration>();
        }

        protected override void OnUpdate()
        {
            Dependency.Complete();
            EntityManager entityManager = EntityManager;
            if (!CombatPhaseRules.IsActive(entityManager))
            {
                entityManager.DestroyEntity(_projectileQuery);
                return;
            }

            using NativeArray<Entity> projectileEntities = _projectileQuery.ToEntityArray(Allocator.Temp);
            using var enemyIndex = new EnemyCombatSpatialIndex(_enemyQuery);
            var candidates = new NativeList<int>(Allocator.Temp);
            using var candidateLifetime = candidates;
            using var entityCommands = new EntityCommandBuffer(Allocator.Temp);
            foreach (Entity projectileEntity in projectileEntities)
            {
                ArenaProjectile projectile = entityManager.GetComponentData<ArenaProjectile>(projectileEntity);
                LocalTransform projectileTransform = entityManager.GetComponentData<LocalTransform>(projectileEntity);
                float movementDeltaTime = math.min(math.max(0, projectile.Remaining), SystemAPI.Time.DeltaTime);
                float3 movementEnd = projectileTransform.Position + projectile.Velocity * movementDeltaTime;
                bool isConsumed = false;
                int maximumHits = math.clamp(projectile.ExtraPierceTargets, 0, ArenaLimits.MAX_PROJECTILE_PIERCE_TARGETS) + 1;
                for (int hitIndex = 0; hitIndex < maximumHits; hitIndex++)
                {
                    if (!TryFindFirstHit(ref projectile, projectileTransform.Position.xz, movementEnd.xz, enemyIndex, ref candidates,
                        out int hitEnemyIndex))
                    {
                        break;
                    }

                    Entity hitEnemy = enemyIndex.Entities[hitEnemyIndex];
                    projectile.HitTargets.Add(hitEnemy);
                    CombatHealth enemyHealth = entityManager.GetComponentData<CombatHealth>(hitEnemy);
                    int previousHealth = enemyHealth.Current;
                    enemyHealth.Current = math.max(0, enemyHealth.Current - projectile.Damage);
                    entityManager.SetComponentData(hitEnemy, enemyHealth);
                    CombatHealthChanges.Send(entityCommands,
                        entityManager.GetComponentData<LocalTransform>(hitEnemy).Position,
                        enemyHealth.Current - previousHealth,
                        false,
                        projectile.IsCritical);
                    if (enemyHealth.Current == 0)
                    {
                        entityCommands.AddComponent(hitEnemy, new EnemyDeath
                        {
                            Killer = projectile.Shooter
                        });
                    }
                    else if (projectile.KnockbackDistance > 0)
                    {
                        LocalTransform enemyTransform = entityManager.GetComponentData<LocalTransform>(hitEnemy);
                        enemyTransform.Position.xz += math.normalizesafe(projectile.Velocity.xz) * projectile.KnockbackDistance;
                        if (SystemAPI.TryGetSingleton(out WaveRuntimeSettings arenaSettings))
                        {
                            float enemyRadius = entityManager.GetComponentData<ArenaEnemy>(hitEnemy).Radius;
                            float2 movementHalfExtents = math.max(float2.zero, arenaSettings.HalfExtents - enemyRadius);
                            enemyTransform.Position.xz = math.clamp(enemyTransform.Position.xz,
                                arenaSettings.Center - movementHalfExtents,
                                arenaSettings.Center + movementHalfExtents);
                        }

                        entityManager.SetComponentData(hitEnemy, enemyTransform);
                        enemyIndex.UpdateTransform(hitEnemyIndex, enemyTransform);
                    }

                    if (projectile.ExtraPierceTargets <= 0)
                    {
                        isConsumed = true;
                        break;
                    }

                    projectile.ExtraPierceTargets--;
                }

                projectile.Remaining -= SystemAPI.Time.DeltaTime;
                if (isConsumed || projectile.Remaining <= 0)
                {
                    entityCommands.DestroyEntity(projectileEntity);
                }
                else
                {
                    projectileTransform.Position = movementEnd;
                    entityManager.SetComponentData(projectileEntity, projectileTransform);
                    entityManager.SetComponentData(projectileEntity, projectile);
                }
            }

            entityCommands.Playback(entityManager);
        }

        private bool TryFindFirstHit(ref ArenaProjectile projectile,
            float2 movementStart,
            float2 movementEnd,
            EnemyCombatSpatialIndex enemyIndex,
            ref NativeList<int> candidates,
            out int hitEnemyIndex)
        {
            hitEnemyIndex = -1;
            float padding = enemyIndex.MaximumRadius + math.max(0, projectile.Radius);
            enemyIndex.CollectCandidates(math.min(movementStart, movementEnd) - padding,
                math.max(movementStart, movementEnd) + padding, ref candidates);
            float earliestHitFraction = float.MaxValue;
            foreach (int candidateIndex in candidates)
            {
                Entity enemyEntity = enemyIndex.Entities[candidateIndex];
                if (projectile.HitTargets.Contains(enemyEntity)
                    || EntityManager.GetComponentData<CombatHealth>(enemyEntity).Current <= 0)
                {
                    continue;
                }

                ArenaEnemy enemy = EntityManager.GetComponentData<ArenaEnemy>(enemyEntity);
                float2 enemyPosition = EntityManager.GetComponentData<LocalTransform>(enemyEntity).Position.xz;
                float collisionRadius = math.max(0, enemy.Radius) + projectile.Radius;
                if (!TryIntersectEnemyFootprint(movementStart, movementEnd, enemyPosition, collisionRadius, out float hitFraction)
                    || hitFraction >= earliestHitFraction)
                {
                    continue;
                }

                earliestHitFraction = hitFraction;
                hitEnemyIndex = candidateIndex;
            }

            return hitEnemyIndex >= 0;
        }

        public static bool TryIntersectEnemyFootprint(float2 movementStart,
            float2 movementEnd,
            float2 enemyCenter,
            float collisionRadius,
            out float hitFraction)
        {
            float2 startOffset = movementStart - enemyCenter;
            float2 movementDelta = movementEnd - movementStart;
            float distanceSquaredFromBoundary = math.lengthsq(startOffset) - collisionRadius * collisionRadius;
            hitFraction = 0;
            if (distanceSquaredFromBoundary <= 0)
            {
                return true;
            }

            float movementLengthSquared = math.lengthsq(movementDelta);
            float startProjection = math.dot(startOffset, movementDelta);
            float discriminant = startProjection * startProjection - movementLengthSquared * distanceSquaredFromBoundary;
            if (movementLengthSquared < MIN_MOVEMENT_SQUARED || discriminant < 0)
            {
                return false;
            }

            hitFraction = (-startProjection - math.sqrt(discriminant)) / movementLengthSquared;
            return hitFraction >= 0 && hitFraction <= 1;
        }
    }
}
