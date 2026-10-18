using System;
using Unity.Core;
using Unity.Entities;
using Unity.Transforms;
using NUnit.Framework;

namespace HordeMuncher.Tests
{
    public sealed class SystemOrderTests
    {
        [Test]
        public void CombatSystemsRespectProductionOrder()
        {
            VerifyOrder();
        }

        private static void VerifyOrder()
        {
            using var world = new World("System order verification");
            EntityManager entityManager = world.EntityManager;
            Entity projectilePrefab = entityManager.CreateEntity(typeof(Prefab), typeof(ArenaProjectile), typeof(LocalTransform));
            Entity resourcePrefab = entityManager.CreateEntity(typeof(Prefab), typeof(ResourcePickup), typeof(LocalTransform));
            entityManager.SetComponentData(resourcePrefab, new ResourcePickup
            {
                Value = 1
            });
            entityManager.SetComponentData(resourcePrefab, LocalTransform.FromScale(0.2f));
            Entity runEntity = entityManager.CreateEntity(typeof(ArenaConfiguration), typeof(WaveProgress));
            entityManager.SetComponentData(runEntity, new ArenaConfiguration
            {
                ProjectilePrefab = projectilePrefab,
                ResourcePrefab = resourcePrefab
            });
            entityManager.SetComponentData(runEntity, new WaveProgress
            {
                Phase = WavePhase.Spawning
            });
            Entity firstPlayer = CreatePlayer(entityManager);
            Entity secondPlayer = CreatePlayer(entityManager);
            Entity enemy = entityManager.CreateEntity(typeof(ArenaEnemy), typeof(CombatHealth), typeof(LocalTransform));
            entityManager.SetComponentData(enemy, new ArenaEnemy
            {
                Radius = 0.3f,
                ContactDamage = 10,
                ContactCooldown = 0.75f
            });
            entityManager.SetComponentData(enemy, new CombatHealth
            {
                Current = 30,
                Maximum = 30
            });
            entityManager.SetComponentData(enemy, LocalTransform.FromPosition(0.75f, 0, 0));
            SimulationSystemGroup group = world.CreateSystemManaged<SimulationSystemGroup>();
            group.AddSystemToUpdateList(world.CreateSystemManaged<ResourcePickupSystem>());
            group.AddSystemToUpdateList(world.CreateSystemManaged<HealthRegenerationSystem>());
            group.AddSystemToUpdateList(world.CreateSystemManaged<ContactDamageSystem>());
            group.AddSystemToUpdateList(world.CreateSystemManaged<WeaponFireSystem>());
            group.AddSystemToUpdateList(world.CreateSystemManaged<EnemyDeathSystem>());
            group.AddSystemToUpdateList(world.CreateSystemManaged<ProjectileSystem>());
            group.SortSystems();
            Step(world, group);
            Require(entityManager.Exists(enemy) && entityManager.GetComponentData<CombatHealth>(enemy).Current == 30,
                "Newly emitted projectiles moved in the same tick");
            Require(entityManager.GetComponentData<CombatHealth>(firstPlayer).Current == 90, "Contact damage did not run after emission");
            Step(world, group);
            Require(!entityManager.Exists(enemy), "Pending death was not resolved before the next shot");
            using EntityQuery resources = entityManager.CreateEntityQuery(typeof(ResourcePickup));
            Require(resources.CalculateEntityCount() == 1, "Simultaneous projectiles created more than one pickup");
            for (int attractionTick = 0; attractionTick < 5; attractionTick++)
            {
                Step(world, group);
            }

            CombatWallet firstWallet = entityManager.GetComponentData<CombatWallet>(firstPlayer);
            CombatWallet secondWallet = entityManager.GetComponentData<CombatWallet>(secondPlayer);
            Require(firstWallet.Kills + secondWallet.Kills == 1 && firstWallet.Resources + secondWallet.Resources == 1,
                "Simultaneous projectiles duplicated the death reward or pickup");
            Require(resources.IsEmptyIgnoreFilter, "Collected resource was not removed");
            Step(world, group);
            Require(entityManager.GetComponentData<CombatWallet>(firstPlayer).Kills
                + entityManager.GetComponentData<CombatWallet>(secondPlayer).Kills
                == 1, "Death reward was processed twice");
            entityManager.SetComponentData(firstPlayer, new CombatHealth
            {
                Current = 50,
                Maximum = 100
            });
            entityManager.AddComponentData(firstPlayer, new PlayerBonuses
            {
                Regeneration = 10
            });
            entityManager.SetComponentData(runEntity, new WaveProgress
            {
                Phase = WavePhase.Shop
            });
            Step(world, group);
            Require(entityManager.GetComponentData<CombatHealth>(firstPlayer).Current == 50, "Regeneration ran inside the shop");
            entityManager.SetComponentData(runEntity, new WaveProgress
            {
                Phase = WavePhase.Spawning
            });
            Step(world, group);
            Require(entityManager.GetComponentData<CombatHealth>(firstPlayer).Current == 51, "Regeneration did not resume");
            entityManager.SetComponentData(firstPlayer, new CombatHealth
            {
                Current = 0,
                Maximum = 100
            });
            Step(world, group);
            Require(entityManager.GetComponentData<CombatHealth>(firstPlayer).Current == 0, "Regeneration resurrected a dead player");
        }

        private static Entity CreatePlayer(EntityManager entityManager)
        {
            Entity player = entityManager.CreateEntity(typeof(ArenaPlayer),
                typeof(CombatHealth),
                typeof(AutoWeapon),
                typeof(CombatWallet),
                typeof(WeaponShot),
                typeof(LocalTransform));
            entityManager.SetComponentData(player, LocalTransform.Identity);
            entityManager.SetComponentData(player, new CombatHealth
            {
                Current = 100,
                Maximum = 100
            });
            entityManager.SetComponentData(player, new AutoWeapon
            {
                Damage = 30,
                Interval = 0.25f,
                Range = 9
            });
            return player;
        }

        private static void Step(World world, SimulationSystemGroup group)
        {
            world.SetTime(new TimeData(world.Time.ElapsedTime + 0.1, 0.1f));
            group.Update();
            world.EntityManager.CompleteAllTrackedJobs();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
