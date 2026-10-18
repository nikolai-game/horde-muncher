using System;
using Unity.Core;
using Unity.Entities;
using Unity.Transforms;

namespace HordeMuncher.Tests
{
    public sealed class CombatTestArena : IDisposable
    {
        private readonly SimulationSystemGroup _combatGroup;
        private double _elapsedTime;
        public World World { get; } = new World("Combat tests");
        public EntityManager EntityManager => World.EntityManager;

        public CombatTestArena()
        {
            Entity resource = EntityManager.CreateEntity(typeof(Prefab), typeof(ResourcePickup), typeof(LocalTransform));
            EntityManager.SetComponentData(resource, new ResourcePickup
            {
                Value = 1
            });
            EntityManager.SetComponentData(resource, LocalTransform.Identity);
            Entity projectile = EntityManager.CreateEntity(typeof(Prefab), typeof(ArenaProjectile), typeof(LocalTransform));
            Entity configurationEntity = EntityManager.CreateEntity(typeof(ArenaConfiguration));
            EntityManager.SetComponentData(configurationEntity, new ArenaConfiguration
            {
                ResourcePrefab = resource,
                ProjectilePrefab = projectile
            });
            _combatGroup = World.CreateSystemManaged<SimulationSystemGroup>();
            _combatGroup.AddSystemToUpdateList(World.CreateSystemManaged<ResourcePickupSystem>());
            _combatGroup.AddSystemToUpdateList(World.CreateSystemManaged<HealthRegenerationSystem>());
            _combatGroup.AddSystemToUpdateList(World.CreateSystemManaged<ContactDamageSystem>());
            _combatGroup.AddSystemToUpdateList(World.CreateSystemManaged<WeaponFireSystem>());
            _combatGroup.AddSystemToUpdateList(World.CreateSystemManaged<EnemyDeathSystem>());
            _combatGroup.AddSystemToUpdateList(World.CreateSystemManaged<ProjectileSystem>());
            _combatGroup.SortSystems();
        }

        public Entity CreatePlayer(float position, int damage = 15, int health = 100)
        {
            Entity player = EntityManager.CreateEntity(typeof(ArenaPlayer),
                typeof(CombatHealth),
                typeof(AutoWeapon),
                typeof(CombatWallet),
                typeof(WeaponShot),
                typeof(LocalTransform));
            EntityManager.SetComponentData(player, LocalTransform.FromPosition(position, 1, 0));
            EntityManager.SetComponentData(player, new CombatHealth
            {
                Current = health,
                Maximum = 100
            });
            EntityManager.SetComponentData(player, new AutoWeapon
            {
                Range = 9,
                Interval = 0.25f,
                Damage = damage
            });
            return player;
        }

        public Entity CreateEnemy(float position, int health = 30)
        {
            Entity enemy = EntityManager.CreateEntity(typeof(ArenaEnemy), typeof(CombatHealth), typeof(LocalTransform));
            EntityManager.SetComponentData(enemy, new ArenaEnemy
            {
                ContactDamage = 10,
                Radius = 0.325f,
                ContactCooldown = 0.75f
            });
            EntityManager.SetComponentData(enemy, LocalTransform.FromPosition(position, 0.65f, 0));
            EntityManager.SetComponentData(enemy, new CombatHealth
            {
                Current = health,
                Maximum = health
            });
            return enemy;
        }

        public int Count<T>()
        where T : unmanaged, IComponentData
        {
            using EntityQuery query = EntityManager.CreateEntityQuery(typeof(T));
            return query.CalculateEntityCount();
        }

        public void Step(float deltaTime = 0.1f)
        {
            _elapsedTime += deltaTime;
            World.SetTime(new TimeData(_elapsedTime, deltaTime));
            _combatGroup.Update();
            EntityManager.CompleteAllTrackedJobs();
        }

        public void Dispose()
        {
            World.Dispose();
        }
    }
}
