using NUnit.Framework;
using Unity.Entities;
using Unity.Transforms;

namespace HordeMuncher.Tests
{
    public sealed class ArenaRunRulesTests
    {
        [TestCase(WavePhase.Waiting)]
        [TestCase(WavePhase.Shop)]
        public void StartingWaveRestoresEachPlayerToCurrentMaximumHealth(WavePhase previousPhase)
        {
            using var world = new World("Wave starting health tests");
            EntityManager entityManager = world.EntityManager;
            Entity run = entityManager.CreateEntity(typeof(WaveProgress), typeof(WaveRuntimeSettings));
            entityManager.SetComponentData(run, new WaveProgress { Phase = previousPhase });
            entityManager.AddBuffer<WavePlan>(run).Add(default);
            Entity firstPlayer = entityManager.CreateEntity(typeof(ArenaPlayer), typeof(CombatHealth),
                typeof(LocalTransform), typeof(PlayerProgress));
            Entity secondPlayer = entityManager.CreateEntity(typeof(ArenaPlayer), typeof(CombatHealth),
                typeof(LocalTransform), typeof(PlayerProgress));
            entityManager.SetComponentData(firstPlayer, new CombatHealth { Current = 17, Maximum = 125, HurtCooldown = 1 });
            entityManager.SetComponentData(secondPlayer, new CombatHealth { Current = 9, Maximum = 80, HurtCooldown = 2 });
            entityManager.SetComponentData(firstPlayer, new PlayerProgress { Ready = true });
            entityManager.SetComponentData(secondPlayer, new PlayerProgress { Ready = true });
            RunLifecycleSystem lifecycleSystem = world.CreateSystemManaged<RunLifecycleSystem>();

            lifecycleSystem.Update();

            Assert.That(entityManager.GetComponentData<WaveProgress>(run).Phase, Is.EqualTo(WavePhase.Spawning));
            Assert.That(entityManager.GetComponentData<CombatHealth>(firstPlayer).Current, Is.EqualTo(125));
            Assert.That(entityManager.GetComponentData<CombatHealth>(secondPlayer).Current, Is.EqualTo(80));
            Assert.That(entityManager.GetComponentData<CombatHealth>(firstPlayer).HurtCooldown, Is.Zero);
            Assert.That(entityManager.GetComponentData<CombatHealth>(secondPlayer).HurtCooldown, Is.Zero);

            entityManager.SetComponentData(firstPlayer, new CombatHealth { Current = 42, Maximum = 125, HurtCooldown = 1 });
            lifecycleSystem.Update();

            Assert.That(entityManager.GetComponentData<CombatHealth>(firstPlayer).Current, Is.EqualTo(42));
            Assert.That(entityManager.GetComponentData<CombatHealth>(firstPlayer).HurtCooldown, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WaveCleanupPreservesCollectingResourcesAndRestartDestroysLinkedEntities(bool restartRun)
        {
            using var world = new World("Arena cleanup tests");
            EntityManager entityManager = world.EntityManager;
            Entity enemyPrefab = CreateLinkedPrefab<ArenaEnemy>(entityManager);
            Entity projectilePrefab = CreateLinkedPrefab<ArenaProjectile>(entityManager);
            Entity resourcePrefab = CreateLinkedPrefab<ResourcePickup>(entityManager);
            entityManager.SetComponentData(resourcePrefab, new ResourcePickup { Value = 7 });
            Entity enemy = entityManager.Instantiate(enemyPrefab);
            Entity projectile = entityManager.Instantiate(projectilePrefab);
            Entity resource = entityManager.Instantiate(resourcePrefab);
            Entity enemyChild = entityManager.GetBuffer<LinkedEntityGroup>(enemy)[1].Value;
            Entity projectileChild = entityManager.GetBuffer<LinkedEntityGroup>(projectile)[1].Value;
            Entity resourceChild = entityManager.GetBuffer<LinkedEntityGroup>(resource)[1].Value;
            Entity player = entityManager.CreateEntity(typeof(ArenaPlayer), typeof(CombatWallet));
            entityManager.SetComponentData(player, new CombatWallet { Resources = 3 });
            Entity run = entityManager.CreateEntity(typeof(WaveProgress));
            entityManager.SetComponentData(run, new WaveProgress { Phase = WavePhase.Defeat, Index = 2 });

            if (restartRun)
            {
                ArenaRunRules.RestartRun(entityManager, run, default);
                Assert.That(entityManager.GetComponentData<WaveProgress>(run).Index, Is.Zero);
                Assert.That(entityManager.GetComponentData<WaveProgress>(run).Phase, Is.EqualTo(default(WavePhase)));
            }
            else
            {
                ArenaRunRules.CompleteWave(entityManager);
                Assert.That(entityManager.GetComponentData<CombatWallet>(player).Resources, Is.EqualTo(3));
                Assert.That(entityManager.GetComponentData<ResourcePickup>(resource).StoreInBag, Is.True);
                Assert.That(entityManager.GetComponentData<ResourcePickup>(resource).Collector, Is.EqualTo(player));
            }

            Assert.That(entityManager.Exists(enemy), Is.False);
            Assert.That(entityManager.Exists(enemyChild), Is.False);
            Assert.That(entityManager.Exists(projectile), Is.False);
            Assert.That(entityManager.Exists(projectileChild), Is.False);
            Assert.That(entityManager.Exists(resource), Is.EqualTo(!restartRun));
            Assert.That(entityManager.Exists(resourceChild), Is.EqualTo(!restartRun));
            Assert.That(entityManager.Exists(player), Is.True);
            Assert.That(entityManager.Exists(enemyPrefab), Is.True);
            Assert.That(entityManager.Exists(projectilePrefab), Is.True);
            Assert.That(entityManager.Exists(resourcePrefab), Is.True);
            Assert.That(entityManager.Exists(entityManager.GetBuffer<LinkedEntityGroup>(enemyPrefab)[1].Value), Is.True);
            Assert.That(entityManager.Exists(entityManager.GetBuffer<LinkedEntityGroup>(projectilePrefab)[1].Value), Is.True);
            Assert.That(entityManager.Exists(entityManager.GetBuffer<LinkedEntityGroup>(resourcePrefab)[1].Value), Is.True);
        }

        private static Entity CreateLinkedPrefab<T>(EntityManager entityManager) where T : unmanaged, IComponentData
        {
            Entity prefab = entityManager.CreateEntity(typeof(Prefab), typeof(T));
            Entity child = entityManager.CreateEntity(typeof(Prefab));
            DynamicBuffer<LinkedEntityGroup> linkedEntities = entityManager.AddBuffer<LinkedEntityGroup>(prefab);
            linkedEntities.Add(new LinkedEntityGroup { Value = prefab });
            linkedEntities.Add(new LinkedEntityGroup { Value = child });
            return prefab;
        }
    }
}
