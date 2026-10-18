using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemyDeathSystem))]
    [UpdateBefore(typeof(ContactDamageSystem))]
    public partial class WeaponFireSystem : SystemBase
    {
        private const uint INITIAL_CRITICAL_RANDOM_SEED = 0xA631F92Du;
        private const float MIN_ATTACK_MULTIPLIER = 0.1f;
        private const int FALLBACK_PROJECTILE_SPEED = 30;
        private const float FALLBACK_PROJECTILE_RADIUS = 0.12f;
        private const int FALLBACK_PROJECTILE_LIFETIME = 2;
        private const float HALF_CONE_FACTOR = 0.5f;
        private const int DIAMETER_FACTOR = 2;

        private EntityQuery _playerQuery;
        private EntityQuery _enemyQuery;
        private Random _criticalRandom = new Random(INITIAL_CRITICAL_RANDOM_SEED);

        protected override void OnCreate()
        {
            _playerQuery = GetEntityQuery(typeof(ArenaPlayer), typeof(CombatHealth), typeof(AutoWeapon), typeof(WeaponShot), typeof(LocalTransform));
            _enemyQuery = GetEntityQuery(typeof(ArenaEnemy), typeof(CombatHealth), typeof(LocalTransform));
            RequireForUpdate<ArenaConfiguration>();
            RequireForUpdate(_playerQuery);
        }

        protected override void OnUpdate()
        {
            if (!CombatPhaseRules.IsActive(EntityManager))
            {
                return;
            }

            Dependency.Complete();
            EntityManager entityManager = EntityManager;
            ArenaConfiguration arenaConfiguration = SystemAPI.GetSingleton<ArenaConfiguration>();
            bool hasEconomyConfiguration = SystemAPI.TryGetSingleton(out EconomyConfig economyConfiguration);
            Entity economyEntity = hasEconomyConfiguration ? SystemAPI.GetSingletonEntity<EconomyConfig>() : Entity.Null;
            bool hasWeaponCatalog = hasEconomyConfiguration && entityManager.HasBuffer<CatalogEntry>(economyEntity);
            DynamicBuffer<CatalogEntry> catalog = hasWeaponCatalog ? entityManager.GetBuffer<CatalogEntry>(economyEntity) : default;
            using NativeArray<Entity> playerEntities = _playerQuery.ToEntityArray(Allocator.Temp);
            using var enemyIndex = new EnemyCombatSpatialIndex(_enemyQuery);
            var candidates = new NativeList<int>(Allocator.Temp);
            using var candidateLifetime = candidates;
            using var entityCommands = new EntityCommandBuffer(Allocator.Temp);
            foreach (Entity playerEntity in playerEntities)
            {
                if (entityManager.GetComponentData<CombatHealth>(playerEntity).Current <= 0)
                {
                    continue;
                }

                float3 playerPosition = entityManager.GetComponentData<LocalTransform>(playerEntity).Position;
                PlayerBonuses playerBonuses = entityManager.HasComponent<PlayerBonuses>(playerEntity)
                    ? entityManager.GetComponentData<PlayerBonuses>(playerEntity)
                    : default;
                if (hasWeaponCatalog && entityManager.HasBuffer<EquippedWeapon>(playerEntity))
                {
                    DynamicBuffer<EquippedWeapon> equippedWeapons = entityManager.GetBuffer<EquippedWeapon>(playerEntity);
                    for (int weaponSlot = 0; weaponSlot < equippedWeapons.Length; weaponSlot++)
                    {
                        EquippedWeapon equippedWeapon = equippedWeapons[weaponSlot];
                        if (equippedWeapon.CatalogId < 0 || equippedWeapon.CatalogId >= catalog.Length)
                        {
                            continue;
                        }

                        CatalogEntry weaponDefinition = catalog[equippedWeapon.CatalogId];
                        equippedWeapon.Cooldown = math.max(0, equippedWeapon.Cooldown - SystemAPI.Time.DeltaTime);
                        if (weaponDefinition.RetargetOnKill && equippedWeapon.LastTarget != Entity.Null
                            && (!entityManager.HasComponent<CombatHealth>(equippedWeapon.LastTarget)
                            || entityManager.GetComponentData<CombatHealth>(equippedWeapon.LastTarget).Current <= 0))
                        {
                            equippedWeapon.Cooldown = 0;
                            equippedWeapon.LastTarget = Entity.Null;
                        }

                        if (equippedWeapon.Cooldown == 0
                            && TryFireWeapon(playerEntity,
                            playerPosition,
                            weaponDefinition,
                            playerBonuses,
                            enemyIndex,
                            ref candidates,
                            entityCommands,
                            arenaConfiguration,
                            economyConfiguration,
                            weaponSlot,
                            equippedWeapons.Length,
                            ref equippedWeapon))
                        {
                            equippedWeapon.Cooldown = weaponDefinition.Interval / math.max(MIN_ATTACK_MULTIPLIER, 1 + playerBonuses.AttackSpeed);
                        }

                        equippedWeapons[weaponSlot] = equippedWeapon;
                    }
                }
                else
                {
                    AutoWeapon weapon = entityManager.GetComponentData<AutoWeapon>(playerEntity);
                    var equippedWeapon = new EquippedWeapon();
                    weapon.Cooldown = math.max(0, weapon.Cooldown - SystemAPI.Time.DeltaTime);
                    var weaponDefinition = new CatalogEntry
                    {
                        Damage = weapon.Damage,
                        Range = weapon.Range,
                        Targets = 1,
                        ProjectileSpeed = FALLBACK_PROJECTILE_SPEED,
                        ProjectileRadius = FALLBACK_PROJECTILE_RADIUS,
                        ProjectileLifetime = FALLBACK_PROJECTILE_LIFETIME
                    };
                    if (weapon.Cooldown == 0
                        && TryFireWeapon(playerEntity,
                        playerPosition,
                        weaponDefinition,
                        playerBonuses,
                        enemyIndex,
                        ref candidates,
                        entityCommands,
                        arenaConfiguration,
                        economyConfiguration,
                        0,
                        1,
                        ref equippedWeapon))
                    {
                        weapon.Cooldown = weapon.Interval;
                    }

                    entityManager.SetComponentData(playerEntity, weapon);
                }
            }

            entityCommands.Playback(entityManager);
        }

        private bool TryFireWeapon(Entity playerEntity,
            float3 playerPosition,
            CatalogEntry weapon,
            PlayerBonuses playerBonuses,
            EnemyCombatSpatialIndex spatialIndex,
            ref NativeList<int> candidates,
            EntityCommandBuffer entityCommands,
            ArenaConfiguration arenaConfiguration,
            EconomyConfig economyConfiguration,
            int weaponSlot,
            int weaponSlotCount,
            ref EquippedWeapon equippedWeapon)
        {
            float attackRange = WeaponCombatRules.CalculateAttackRange(weapon.Range, playerBonuses.Range);
            NativeArray<Entity> enemyEntities = spatialIndex.Entities;
            NativeArray<LocalTransform> enemyTransforms = spatialIndex.Transforms;
            spatialIndex.CollectCandidates(playerPosition.xz - attackRange, playerPosition.xz + attackRange, ref candidates);
            float nearestDistanceSquared = attackRange * attackRange;
            int nearestEnemyIndex = -1;
            foreach (int enemyIndex in candidates)
            {
                if (EntityManager.GetComponentData<CombatHealth>(enemyEntities[enemyIndex]).Current <= 0)
                {
                    continue;
                }

                float distanceSquared = math.distancesq(playerPosition.xz, enemyTransforms[enemyIndex].Position.xz);
                if (distanceSquared > nearestDistanceSquared)
                {
                    continue;
                }

                nearestDistanceSquared = distanceSquared;
                nearestEnemyIndex = enemyIndex;
            }

            bool isMelee = WeaponCombatRules.IsMelee(weapon.Weapon);
            if (nearestEnemyIndex < 0 || (!isMelee && arenaConfiguration.ProjectilePrefab == Entity.Null))
            {
                return false;
            }

            float3 weaponMountPosition = WeaponLayout.CalculateMountPosition(playerPosition,
                weaponSlot,
                weaponSlotCount,
                economyConfiguration.WeaponOrbitRadius,
                economyConfiguration.WeaponHeight);
            float3 targetPosition = enemyTransforms[nearestEnemyIndex].Position;
            float3 playerToTarget = new float3(targetPosition.x - playerPosition.x, 0, targetPosition.z - playerPosition.z);
            float3 mountToTarget = new float3(targetPosition.x - weaponMountPosition.x, 0, targetPosition.z - weaponMountPosition.z);
            float3 aimDirection = math.normalizesafe(playerToTarget, new float3(0, 0, 1));
            aimDirection = math.normalizesafe(mountToTarget, aimDirection);
            if (isMelee)
            {
                aimDirection = math.normalizesafe(playerToTarget, new float3(0, 0, 1));
            }

            equippedWeapon.Aim = aimDirection;
            equippedWeapon.LastTarget = enemyEntities[nearestEnemyIndex];
            float3 muzzlePosition = weaponMountPosition + math.rotate(quaternion.LookRotationSafe(aimDirection, math.up()), weapon.MuzzleOffset);
            int projectileCount = weapon.Weapon == WeaponKind.Shotgun ? math.clamp(weapon.Targets, 1, ArenaLimits.MAX_SHOTGUN_PELLETS) : 1;
            if (isMelee)
            {
                ApplyMeleeStrike(playerEntity, playerPosition, aimDirection, attackRange, nearestEnemyIndex,
                    weapon, playerBonuses, spatialIndex, candidates, entityCommands);
            }

            for (int projectileIndex = 0; !isMelee && projectileIndex < projectileCount; projectileIndex++)
            {
                float spreadAngle = projectileCount == 1
                    ? 0 : math.radians(weapon.Cone) * (projectileIndex / (float)(projectileCount - 1) - HALF_CONE_FACTOR);
                float3 projectileDirection = math.rotate(quaternion.RotateY(spreadAngle), aimDirection);
                bool isCritical = _criticalRandom.NextFloat() < math.saturate(playerBonuses.CriticalChance);
                Entity projectileEntity = entityCommands.Instantiate(arenaConfiguration.ProjectilePrefab);
                entityCommands.SetComponent(projectileEntity,
                    LocalTransform.FromPositionRotationScale(muzzlePosition,
                    quaternion.LookRotationSafe(projectileDirection, math.up()),
                    weapon.ProjectileRadius * DIAMETER_FACTOR));
                entityCommands.SetComponent(projectileEntity, new ArenaProjectile
                {
                    Shooter = playerEntity,
                    Velocity = projectileDirection * weapon.ProjectileSpeed,
                    Radius = weapon.ProjectileRadius,
                    Remaining = math.min(weapon.ProjectileLifetime, attackRange / math.max(MIN_ATTACK_MULTIPLIER, weapon.ProjectileSpeed)),
                    ExtraPierceTargets = math.clamp(weapon.ExtraPierceTargets, 0, ArenaLimits.MAX_PROJECTILE_PIERCE_TARGETS),
                    KnockbackDistance = math.max(0, weapon.KnockbackDistance),
                    Damage = WeaponCombatRules.CalculateDamage(weapon, playerBonuses, isCritical),
                    IsCritical = isCritical
                });
            }

            equippedWeapon.AttackSequence++;
            WeaponShot shotPresentation = EntityManager.GetComponentData<WeaponShot>(playerEntity);
            shotPresentation.Sequence++;
            shotPresentation.Origin = muzzlePosition;
            shotPresentation.Target = enemyTransforms[nearestEnemyIndex].Position;
            EntityManager.SetComponentData(playerEntity, shotPresentation);
            return true;
        }

        private void ApplyMeleeStrike(Entity playerEntity,
            float3 playerPosition,
            float3 aimDirection,
            float attackRange,
            int nearestEnemyIndex,
            CatalogEntry weapon,
            PlayerBonuses playerBonuses,
            EnemyCombatSpatialIndex spatialIndex,
            NativeList<int> candidates,
            EntityCommandBuffer entityCommands)
        {
            NativeArray<Entity> enemyEntities = spatialIndex.Entities;
            NativeArray<LocalTransform> enemyTransforms = spatialIndex.Transforms;
            int hitCount = 0;
            float minimumAimDot = math.cos(math.radians(weapon.Cone * HALF_CONE_FACTOR));
            for (int hitCandidate = -1; hitCandidate < candidates.Length && hitCount < math.max(1, weapon.Targets); hitCandidate++)
            {
                int enemyIndex = hitCandidate < 0 ? nearestEnemyIndex : candidates[hitCandidate];
                if (hitCandidate >= 0 && enemyIndex == nearestEnemyIndex)
                {
                    continue;
                }

                Entity enemyEntity = enemyEntities[enemyIndex];
                CombatHealth health = EntityManager.GetComponentData<CombatHealth>(enemyEntity);
                float3 toEnemy = enemyTransforms[enemyIndex].Position - playerPosition;
                toEnemy.y = 0;
                if (health.Current <= 0 || math.lengthsq(toEnemy) > attackRange * attackRange
                    || math.dot(math.normalizesafe(toEnemy, aimDirection), aimDirection) < minimumAimDot)
                {
                    continue;
                }

                bool isCritical = _criticalRandom.NextFloat() < math.saturate(playerBonuses.CriticalChance);
                int previousHealth = health.Current;
                health.Current = math.max(0, health.Current - WeaponCombatRules.CalculateDamage(weapon, playerBonuses, isCritical));
                EntityManager.SetComponentData(enemyEntity, health);
                CombatHealthChanges.Send(entityCommands, enemyTransforms[enemyIndex].Position,
                    health.Current - previousHealth, false, isCritical);
                if (health.Current == 0)
                {
                    entityCommands.AddComponent(enemyEntity, new EnemyDeath { Killer = playerEntity });
                }
                else if (weapon.KnockbackDistance > 0)
                {
                    LocalTransform enemyTransform = EntityManager.GetComponentData<LocalTransform>(enemyEntity);
                    enemyTransform.Position.xz += math.normalizesafe(toEnemy.xz) * weapon.KnockbackDistance;
                    if (SystemAPI.TryGetSingleton(out WaveRuntimeSettings arenaSettings))
                    {
                        float enemyRadius = EntityManager.GetComponentData<ArenaEnemy>(enemyEntity).Radius;
                        float2 movementHalfExtents = math.max(float2.zero, arenaSettings.HalfExtents - enemyRadius);
                        enemyTransform.Position.xz = math.clamp(enemyTransform.Position.xz,
                            arenaSettings.Center - movementHalfExtents, arenaSettings.Center + movementHalfExtents);
                    }

                    EntityManager.SetComponentData(enemyEntity, enemyTransform);
                    spatialIndex.UpdateTransform(enemyIndex, enemyTransform);
                }

                hitCount++;
            }
        }
    }
}
