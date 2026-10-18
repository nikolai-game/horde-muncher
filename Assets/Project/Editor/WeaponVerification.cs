using System;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;

namespace HordeMuncher.Editor
{
    public static class WeaponVerification
    {
        [MenuItem("Horde Muncher/Verify Weapon Rules")]
        public static void RunMenu() => UnityEngine.Debug.Log(Run());

        public static string Run()
        {
            void Require(bool condition, string message)
            {
                if (!condition)
                {
                    throw new Exception(message);
                }
            }

            using var world = new World("Projectile checks");
            EntityManager entityManager = world.EntityManager;
            Entity projectilePrefab = entityManager.CreateEntity(typeof(Prefab), typeof(ArenaProjectile), typeof(LocalTransform));
            Entity configurationEntity = entityManager.CreateEntity(typeof(ArenaConfiguration), typeof(EconomyConfig), typeof(WaveProgress));
            entityManager.SetComponentData(configurationEntity, new ArenaConfiguration
            {
                ProjectilePrefab = projectilePrefab
            });
            entityManager.SetComponentData(configurationEntity, new EconomyConfig
            {
                FirstLevelXp = 1,
                WeaponOrbitRadius = 1,
                WeaponHeight = 0.2f
            });
            entityManager.SetComponentData(configurationEntity, new WaveProgress
            {
                Phase = WavePhase.Spawning
            });
            entityManager.AddBuffer<CatalogEntry>(configurationEntity).Add(new CatalogEntry
            {
                Weapon = WeaponKind.Pistol,
                Damage = 15,
                Interval = 10,
                Range = 9,
                Targets = 1,
                ProjectileSpeed = 10,
                ProjectileRadius = 0.1f,
                ProjectileLifetime = 2,
                MuzzleOffset = new float3(0, 0, 0.5f)
            });
            Entity player = entityManager.CreateEntity(typeof(ArenaPlayer),
                typeof(CombatHealth),
                typeof(AutoWeapon),
                typeof(CombatWallet),
                typeof(WeaponShot),
                typeof(LocalTransform),
                typeof(PlayerProgress),
                typeof(PlayerBonuses));
            entityManager.SetComponentData(player, new CombatHealth
            {
                Current = 100,
                Maximum = 100
            });
            entityManager.SetComponentData(player, LocalTransform.Identity);
            entityManager.SetComponentData(player, new PlayerProgress
            {
                Initialized = true,
                Level = 1,
                NextLevelXp = 1
            });
            entityManager.AddBuffer<EquippedWeapon>(player).Add(new EquippedWeapon
            {
                CatalogId = 0
            });
            Entity CreateEnemy(float positionX, int enemyHealth)
            {
                Entity enemy = entityManager.CreateEntity(typeof(ArenaEnemy), typeof(CombatHealth), typeof(LocalTransform));
                entityManager.SetComponentData(enemy, new ArenaEnemy
                {
                    Experience = 1,
                    Radius = 0.3f
                });
                entityManager.SetComponentData(enemy, new CombatHealth
                {
                    Current = enemyHealth,
                    Maximum = enemyHealth
                });
                entityManager.SetComponentData(enemy, LocalTransform.FromPosition(positionX, 0, 0));
                return enemy;
            }

            Entity nearEnemy = CreateEnemy(5, 15);
            Entity farEnemy = CreateEnemy(6, 100);
            WeaponFireSystem weaponFireSystem = world.CreateSystemManaged<WeaponFireSystem>();
            ProjectileSystem projectileSystem = world.CreateSystemManaged<ProjectileSystem>();
            EnemyDeathSystem enemyDeathSystem = world.CreateSystemManaged<EnemyDeathSystem>();
            void AdvanceTime(float deltaTime) => world.SetTime(new TimeData(world.Time.ElapsedTime + deltaTime, deltaTime));
            using EntityQuery projectileQuery = entityManager.CreateEntityQuery(typeof(ArenaProjectile));
            AdvanceTime(0.1f);
            weaponFireSystem.Update();
            Require(entityManager.GetComponentData<CombatHealth>(nearEnemy).Current == 15, "Damage was instant");
            Require(projectileQuery.CalculateEntityCount() == 1, "No projectile spawned");
            Entity projectile = projectileQuery.GetSingletonEntity();
            Require(math.distance(entityManager.GetComponentData<LocalTransform>(projectile).Position, new float3(1.5f, 0.2f, 0)) < 0.001f,
                "Muzzle mismatch");
            projectileSystem.Update();
            enemyDeathSystem.Update();
            Require(math.abs(entityManager.GetComponentData<LocalTransform>(projectile).Position.x - 2.5f) < 0.001f,
                "Projectile speed mismatch");
            Require(entityManager.Exists(nearEnemy), "Projectile hit before arrival");
            AdvanceTime(0.5f);
            projectileSystem.Update();
            enemyDeathSystem.Update();
            Require(!entityManager.Exists(nearEnemy) && projectileQuery.CalculateEntityCount() == 0
                && entityManager.GetComponentData<CombatHealth>(farEnemy).Current == 100, "Swept collision did not hit first enemy exactly once");
            Require(entityManager.GetComponentData<CombatWallet>(player).Kills == 1
                && entityManager.GetComponentData<PlayerProgress>(player).PendingUpgrades == 1, "Kill XP mismatch");
            CatalogEntry shotgunCatalogEntry = entityManager.GetBuffer<CatalogEntry>(configurationEntity)[0];
            shotgunCatalogEntry.Weapon = WeaponKind.Shotgun;
            shotgunCatalogEntry.Targets = 5;
            shotgunCatalogEntry.Cone = 50;
            DynamicBuffer<CatalogEntry> catalog = entityManager.GetBuffer<CatalogEntry>(configurationEntity);
            catalog[0] = shotgunCatalogEntry;
            DynamicBuffer<EquippedWeapon> inventory = entityManager.GetBuffer<EquippedWeapon>(player);
            inventory[0] = new EquippedWeapon
            {
                CatalogId = 0
            };
            AdvanceTime(0.1f);
            weaponFireSystem.Update();
            Require(projectileQuery.CalculateEntityCount() == 5, "Shotgun did not emit five pellets");
            using (NativeArray<ArenaProjectile> projectiles = projectileQuery.ToComponentDataArray<ArenaProjectile>(Allocator.Temp))
            {
                Require(math.distance(projectiles[0].Velocity, projectiles[4].Velocity) > 1, "Pellet spread missing");
            }

            entityManager.SetComponentData(configurationEntity, new WaveProgress
            {
                Phase = WavePhase.Shop
            });
            projectileSystem.Update();
            enemyDeathSystem.Update();
            weaponFireSystem.Update();
            Require(projectileQuery.CalculateEntityCount() == 0, "Projectiles survived wave transition");
            entityManager.SetComponentData(configurationEntity, new WaveProgress
            {
                Phase = WavePhase.Spawning
            });
            entityManager.DestroyEntity(farEnemy);
            Entity missedProjectile = entityManager.Instantiate(projectilePrefab);
            entityManager.SetComponentData(missedProjectile, LocalTransform.Identity);
            entityManager.SetComponentData(missedProjectile, new ArenaProjectile
            {
                Velocity = new float3(10, 0, 0),
                Remaining = 0.2f,
                Radius = 0.1f
            });
            AdvanceTime(1);
            projectileSystem.Update();
            enemyDeathSystem.Update();
            Require(!entityManager.Exists(missedProjectile), "Missed projectile never expired");
            Require(ProjectileSystem.TryIntersectEnemyFootprint(new float2(0, 0), new float2(10, 0), new float2(5, 0), 0.3f, out _),
                "Low FPS tunneling");
            Require(!ProjectileSystem.TryIntersectEnemyFootprint(new float2(0, 0), new float2(10, 0), new float2(5, 1), 0.3f, out _),
                "False collision");
            return "Passed: muzzle position, delayed damage, speed, swept first hit, XP once, shotgun spread, wave cleanup, expiry.";
        }
    }
}
