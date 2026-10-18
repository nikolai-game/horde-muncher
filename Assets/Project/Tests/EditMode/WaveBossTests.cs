using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HordeMuncher.Tests
{
    public sealed class WaveBossTests
    {
        private World _world;
        private Entity _run;
        private Entity _bossPrefab;
        private WaveSpawnSystem _spawning;
        private WaveCompletionSystem _completion;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Wave boss tests");
            EntityManager entityManager = _world.EntityManager;
            _run = entityManager.CreateEntity(typeof(WaveRuntimeSettings), typeof(WaveProgress));
            entityManager.SetComponentData(_run, new WaveRuntimeSettings
            {
                HalfExtents = new float2(19),
                SafeDistance = 6,
                MaxAlive = 1,
                MaxSpawnsPerTick = 16,
                PositionAttempts = 24
            });
            entityManager.SetComponentData(_run, new WaveProgress
            {
                Phase = WavePhase.Spawning,
                Elapsed = 9,
                RandomState = 17
            });
            _bossPrefab = entityManager.CreateEntity(typeof(Prefab), typeof(ArenaEnemy), typeof(CombatHealth), typeof(LocalTransform));
            entityManager.SetComponentData(_bossPrefab, new ArenaEnemy { Radius = 1.3f, SpawnHeight = 2.6f });
            entityManager.SetComponentData(_bossPrefab, new CombatHealth { Current = 2000, Maximum = 2000 });
            entityManager.SetComponentData(_bossPrefab, LocalTransform.Identity);
            entityManager.AddBuffer<WavePlan>(_run).Add(new WavePlan
            {
                Duration = 10,
                SpawnRatePointCount = 2,
                BossPrefab = _bossPrefab
            });
            DynamicBuffer<WaveSpawnRateCurvePoint> rates = entityManager.AddBuffer<WaveSpawnRateCurvePoint>(_run);
            rates.Add(new WaveSpawnRateCurvePoint());
            rates.Add(new WaveSpawnRateCurvePoint());
            entityManager.AddBuffer<WaveEnemy>(_run);
            Entity player = entityManager.CreateEntity(typeof(ArenaPlayer), typeof(CombatHealth), typeof(LocalTransform), typeof(WaveStatus));
            entityManager.SetComponentData(player, new CombatHealth { Current = 100 });
            entityManager.SetComponentData(player, LocalTransform.Identity);
            _spawning = _world.CreateSystemManaged<WaveSpawnSystem>();
            _completion = _world.CreateSystemManaged<WaveCompletionSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(4, false)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(4, true)]
        public void SpawnBudgetAndPopulationCapScaleWithLivingPlayers(int livingPlayers, bool limitPopulation)
        {
            EntityManager manager = _world.EntityManager;
            WaveRuntimeSettings settings = manager.GetComponentData<WaveRuntimeSettings>(_run);
            settings.MaxAlive = limitPopulation ? 1 : 100;
            manager.SetComponentData(_run, settings);
            WaveProgress progress = manager.GetComponentData<WaveProgress>(_run);
            progress.Elapsed = 0;
            manager.SetComponentData(_run, progress);
            DynamicBuffer<WavePlan> plans = manager.GetBuffer<WavePlan>(_run);
            WavePlan plan = plans[0];
            plan.EnemyCount = 1;
            plan.BossPrefab = Entity.Null;
            plans[0] = plan;
            DynamicBuffer<WaveEnemy> enemyChoices = manager.GetBuffer<WaveEnemy>(_run);
            enemyChoices.Add(new WaveEnemy { Prefab = _bossPrefab });
            DynamicBuffer<WaveSpawnRateCurvePoint> rates = manager.GetBuffer<WaveSpawnRateCurvePoint>(_run);
            rates[0] = new WaveSpawnRateCurvePoint { Rate = 2 };
            rates[1] = new WaveSpawnRateCurvePoint { Rate = 2 };
            for (int playerIndex = 1; playerIndex <= livingPlayers; playerIndex++)
            {
                Entity player = manager.CreateEntity(typeof(ArenaPlayer), typeof(CombatHealth), typeof(LocalTransform));
                manager.SetComponentData(player, new CombatHealth { Current = playerIndex < livingPlayers ? 100 : 0 });
                manager.SetComponentData(player, LocalTransform.Identity);
            }

            Step(1);

            using EntityQuery pendingSpawns = manager.CreateEntityQuery(typeof(PendingEnemySpawn));
            Assert.That(pendingSpawns.CalculateEntityCount(), Is.EqualTo(livingPlayers * (limitPopulation ? 1 : 2)));
        }

        [Test]
        public void BossSpawnsOnceAfterTimerEvenAtPopulationCapAndBlocksVictory()
        {
            EntityManager entityManager = _world.EntityManager;
            Entity survivor = entityManager.Instantiate(_bossPrefab);
            Step(0.5f);
            Assert.That(entityManager.GetComponentData<WaveProgress>(_run).BossSpawned, Is.False);
            Step(0.5f);
            Assert.That(entityManager.GetComponentData<WaveProgress>(_run).BossSpawned, Is.False);
            Step(1.5f);
            WaveProgress progress = entityManager.GetComponentData<WaveProgress>(_run);
            Assert.That(progress.BossSpawned, Is.True);
            Assert.That(progress.Phase, Is.EqualTo(WavePhase.Spawning));
            Assert.That(entityManager.Exists(survivor), Is.True);
            Assert.That(math.length(entityManager.GetComponentData<LocalTransform>(progress.Boss).Position.xz), Is.GreaterThanOrEqualTo(7.3f));
            Step(1);
            Assert.That(entityManager.GetComponentData<WaveProgress>(_run).Boss, Is.EqualTo(progress.Boss));
            using EntityQuery enemies = entityManager.CreateEntityQuery(typeof(ArenaEnemy));
            Assert.That(enemies.CalculateEntityCount(), Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BossDeathCompletesFinalWaveAndCleansUpRemainingEnemies(bool destroyBoss)
        {
            EntityManager entityManager = _world.EntityManager;
            Step(1);
            Step(1.5f);
            Entity boss = entityManager.GetComponentData<WaveProgress>(_run).Boss;
            Entity survivor = entityManager.Instantiate(_bossPrefab);
            if (destroyBoss)
            {
                entityManager.DestroyEntity(boss);
            }
            else
            {
                entityManager.SetComponentData(boss, new CombatHealth { Current = 0, Maximum = 2000 });
            }

            Step(0.1f);
            Assert.That(entityManager.GetComponentData<WaveProgress>(_run).Phase, Is.EqualTo(WavePhase.WaveCleared));
            Assert.That(entityManager.Exists(survivor), Is.False);
            Step(ArenaRunRules.RESOURCE_COLLECTION_DURATION);
            Assert.That(entityManager.GetComponentData<WaveProgress>(_run).Phase, Is.EqualTo(WavePhase.Complete));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClearedWaveRemovesEnemiesImmediatelyAndWaitsBeforeShopOrVictory(bool isFinalWave)
        {
            EntityManager entityManager = _world.EntityManager;
            WaveRuntimeSettings settings = entityManager.GetComponentData<WaveRuntimeSettings>(_run);
            settings.CompletionPauseDuration = 3;
            entityManager.SetComponentData(_run, settings);
            if (!isFinalWave)
            {
                entityManager.GetBuffer<WavePlan>(_run).Add(new WavePlan { Duration = 10 });
            }

            Step(1);
            Step(1.5f);
            Entity boss = entityManager.GetComponentData<WaveProgress>(_run).Boss;
            entityManager.DestroyEntity(boss);
            Entity survivor = entityManager.Instantiate(_bossPrefab);
            Step(0.1f);
            Assert.That(entityManager.GetComponentData<WaveProgress>(_run).Phase, Is.EqualTo(WavePhase.WaveCleared));
            Assert.That(entityManager.Exists(survivor), Is.False);
            using EntityQuery players = entityManager.CreateEntityQuery(typeof(ArenaPlayer), typeof(WaveStatus));
            Assert.That(players.GetSingleton<WaveStatus>().Phase, Is.EqualTo(WavePhase.WaveCleared));
            Step(2.9f);
            Assert.That(entityManager.GetComponentData<WaveProgress>(_run).Phase, Is.EqualTo(WavePhase.WaveCleared));
            Step(0.2f);
            Assert.That(entityManager.GetComponentData<WaveProgress>(_run).Phase,
                Is.EqualTo(isFinalWave ? WavePhase.Complete : WavePhase.Rewards));
        }

        [Test]
        public void UnsafeBossPlacementRetriesWithoutGrantingVictory()
        {
            EntityManager entityManager = _world.EntityManager;
            WaveRuntimeSettings settings = entityManager.GetComponentData<WaveRuntimeSettings>(_run);
            settings.SafeDistance = 100;
            entityManager.SetComponentData(_run, settings);
            Step(1);
            WaveProgress progress = entityManager.GetComponentData<WaveProgress>(_run);
            Assert.That(progress.BossSpawned, Is.False);
            Assert.That(progress.Phase, Is.EqualTo(WavePhase.Spawning));
            settings.SafeDistance = 6;
            entityManager.SetComponentData(_run, settings);
            Step(0.1f);
            Assert.That(entityManager.GetComponentData<WaveProgress>(_run).BossSpawned, Is.False);
            Step(1.5f);
            Assert.That(entityManager.GetComponentData<WaveProgress>(_run).BossSpawned, Is.True);
        }

        [Test]
        public void DefeatDuringBossFightDoesNotSpawnAgainOrGrantVictory()
        {
            EntityManager entityManager = _world.EntityManager;
            Step(1);
            Step(1.5f);
            WaveProgress progress = entityManager.GetComponentData<WaveProgress>(_run);
            entityManager.DestroyEntity(progress.Boss);
            progress.Phase = WavePhase.Defeat;
            entityManager.SetComponentData(_run, progress);
            Step(0.1f);
            Assert.That(entityManager.GetComponentData<WaveProgress>(_run).Phase, Is.EqualTo(WavePhase.Defeat));
        }

        [Test]
        public void PlayerEnteringWarningMovesSpawnNearbyAndRestartsWarning()
        {
            EntityManager entityManager = _world.EntityManager;
            Step(1);
            using EntityQuery pendingQuery = entityManager.CreateEntityQuery(typeof(PendingEnemySpawn));
            PendingEnemySpawn original = pendingQuery.GetSingleton<PendingEnemySpawn>();
            using EntityQuery players = entityManager.CreateEntityQuery(typeof(ArenaPlayer), typeof(LocalTransform));
            entityManager.SetComponentData(players.GetSingletonEntity(),
                LocalTransform.FromPosition(new float3(original.Position.x, 1, original.Position.y)));
            Step(1.5f);
            Assert.That(entityManager.GetComponentData<WaveProgress>(_run).BossSpawned, Is.False);
            PendingEnemySpawn relocated = pendingQuery.GetSingleton<PendingEnemySpawn>();
            Assert.That(math.distance(original.Position, relocated.Position), Is.InRange(2.05f, 6.6f));
            Assert.That(relocated.Remaining, Is.EqualTo(1.5f));
            Step(1);
            Assert.That(entityManager.GetComponentData<WaveProgress>(_run).BossSpawned, Is.False);
            Step(0.5f);
            Entity boss = entityManager.GetComponentData<WaveProgress>(_run).Boss;
            Assert.That(entityManager.GetComponentData<LocalTransform>(boss).Position.xz, Is.EqualTo(relocated.Position));
        }

        [Test]
        public void NoNearbySpaceKeepsSpawnPendingAndDefeatCancelsIt()
        {
            EntityManager entityManager = _world.EntityManager;
            Step(1);
            using EntityQuery pendingQuery = entityManager.CreateEntityQuery(typeof(PendingEnemySpawn));
            PendingEnemySpawn pending = pendingQuery.GetSingleton<PendingEnemySpawn>();
            WaveRuntimeSettings settings = entityManager.GetComponentData<WaveRuntimeSettings>(_run);
            settings.Center = pending.Position;
            settings.HalfExtents = new float2(1.4f);
            entityManager.SetComponentData(_run, settings);
            using EntityQuery players = entityManager.CreateEntityQuery(typeof(ArenaPlayer), typeof(LocalTransform));
            entityManager.SetComponentData(players.GetSingletonEntity(),
                LocalTransform.FromPosition(new float3(pending.Position.x, 1, pending.Position.y)));
            Step(1.5f);
            Assert.That(entityManager.GetComponentData<WaveProgress>(_run).BossSpawned, Is.False);
            Assert.That(pendingQuery.CalculateEntityCount(), Is.EqualTo(1));
            WaveProgress progress = entityManager.GetComponentData<WaveProgress>(_run);
            progress.Phase = WavePhase.Defeat;
            entityManager.SetComponentData(_run, progress);
            Step(0.1f);
            Assert.That(pendingQuery.IsEmptyIgnoreFilter, Is.True);
        }

        [Test]
        public void RegularEnemyWaitsForWarningAndReservesPopulationCapacity()
        {
            EntityManager entityManager = _world.EntityManager;
            DynamicBuffer<WavePlan> plans = entityManager.GetBuffer<WavePlan>(_run);
            WavePlan plan = plans[0];
            plan.Duration = 100;
            plan.BossPrefab = Entity.Null;
            plan.EnemyCount = 1;
            plans[0] = plan;
            entityManager.GetBuffer<WaveEnemy>(_run).Add(new WaveEnemy { Prefab = _bossPrefab });
            DynamicBuffer<WaveSpawnRateCurvePoint> rates = entityManager.GetBuffer<WaveSpawnRateCurvePoint>(_run);
            rates[0] = new WaveSpawnRateCurvePoint { Rate = 2 };
            rates[1] = new WaveSpawnRateCurvePoint { Rate = 2 };
            Step(0.5f);
            using EntityQuery enemies = entityManager.CreateEntityQuery(typeof(ArenaEnemy));
            using EntityQuery pending = entityManager.CreateEntityQuery(typeof(PendingEnemySpawn));
            Assert.That(enemies.CalculateEntityCount(), Is.Zero);
            Assert.That(pending.CalculateEntityCount(), Is.EqualTo(1));
            Step(1);
            Assert.That(enemies.CalculateEntityCount(), Is.Zero);
            Assert.That(pending.CalculateEntityCount(), Is.EqualTo(1));
            Step(0.5f);
            Assert.That(enemies.CalculateEntityCount(), Is.EqualTo(1));
            Assert.That(pending.CalculateEntityCount(), Is.Zero);
        }

        [Test]
        public void ProjectWavePressureGrowsGraduallyWhilePurchasingPowerImproves()
        {
            WaveSettings settings = AssetDatabase.LoadAssetAtPath<WaveSettings>("Assets/Project/Configs/ArenaWaves.asset");
            EconomySettings economy = AssetDatabase.LoadAssetAtPath<EconomySettings>("Assets/Project/Configs/Economy.asset");
            EntityManager manager = _world.EntityManager;
            DynamicBuffer<WavePlan> plans = manager.GetBuffer<WavePlan>(_run);
            DynamicBuffer<WaveSpawnRateCurvePoint> rates = manager.GetBuffer<WaveSpawnRateCurvePoint>(_run);
            plans.Clear();
            rates.Clear();
            foreach (WaveDefinition wave in settings.Waves)
            {
                plans.Add(new WavePlan { Duration = wave.Duration, SpawnRatePointOffset = rates.Length, SpawnRatePointCount = 129 });
                for (int curvePointIndex = 0; curvePointIndex <= 128; curvePointIndex++)
                {
                    rates.Add(new WaveSpawnRateCurvePoint { Rate = wave.SpawnRate.Evaluate(curvePointIndex / 128f) });
                }
            }

            float previousPressure = 0;
            float previousPurchasingPower = 0;
            int typicalItemPrice = economy.Items.Select(item => item.Price).OrderBy(price => price).ElementAt(economy.Items.Length / 2);
            for (int waveIndex = 0; waveIndex < plans.Length; waveIndex++)
            {
                WaveDefinition wave = settings.Waves[waveIndex];
                EnemyDefinition[] enemies = wave.EnemyPrefabs.Select(prefab => prefab.GetComponent<EnemyAuthoring>().Definition).ToArray();
                float enemyCount = WaveMath.CalculateSpawnAmount(rates, plans[waveIndex], 0, wave.Duration);
                float averageHealth = enemies.Average(enemy => math.round(enemy.Health * (1 + settings.EnemyHealthGrowthPerWave * waveIndex)));
                float healthPerSecond = enemyCount * averageHealth / wave.Duration;
                float income = enemyCount * enemies.Average(enemy => (float)enemy.ResourceReward);
                float priceMultiplier = EconomyRules.CalculateWavePriceMultiplier(plans, rates, waveIndex, economy.EnemyPriceGrowth);
                float purchasingPower = income / EconomyRules.CalculatePrice(typicalItemPrice, priceMultiplier);
                if (waveIndex > 0)
                {
                    Assert.That(healthPerSecond, Is.GreaterThan(previousPressure).And.LessThanOrEqualTo(previousPressure * 1.5f),
                        "Wave " + (waveIndex + 1) + " should avoid a sudden jump in enemy health arriving per second.");
                    Assert.That(purchasingPower, Is.GreaterThanOrEqualTo(previousPurchasingPower),
                        "Wave " + (waveIndex + 1) + " should preserve the softer economy despite its changed spawn rate.");
                }

                previousPressure = healthPerSecond;
                previousPurchasingPower = purchasingPower;
            }

            Assert.That(settings.EnemyHealthGrowthPerWave * (plans.Length - 1) + 1, Is.LessThan(3));
            Assert.That(previousPurchasingPower, Is.GreaterThan(3.5f));
        }

        [TestCase(0, false, 2000, 10, 2.5f)]
        [TestCase(4, false, 3440, 13, 2.7f)]
        [TestCase(9, false, 5240, 17, 2.95f)]
        [TestCase(9, true, 5240, 17, 2.95f)]
        [TestCase(19, false, 8840, 25, 3f)]
        [TestCase(1, false, 35, 1, 2.55f, 30, 1)]
        [TestCase(6, false, 62, 1, 2.8f, 30, 1)]
        [TestCase(7, false, 68, 2, 2.85f, 30, 1)]
        public void SpawnScalesStatsByWaveWithoutChangingPrefab(int waveIndex, bool isBoss,
            int expectedHealth, int expectedDamage, float expectedSpeed, int baseHealth = 2000, int baseDamage = 10)
        {
            EntityManager entityManager = _world.EntityManager;
            WaveRuntimeSettings settings = entityManager.GetComponentData<WaveRuntimeSettings>(_run);
            settings.EnemyHealthGrowthPerWave = 0.18f;
            settings.EnemyDamageGrowthPerWave = 0.08f;
            settings.EnemySpeedGrowthPerWave = 0.02f;
            entityManager.SetComponentData(_run, settings);
            ArenaEnemy prefabEnemy = entityManager.GetComponentData<ArenaEnemy>(_bossPrefab);
            prefabEnemy.Speed = 2.5f;
            prefabEnemy.ContactDamage = baseDamage;
            entityManager.SetComponentData(_bossPrefab, new CombatHealth { Current = baseHealth, Maximum = baseHealth });
            entityManager.SetComponentData(_bossPrefab, prefabEnemy);

            DynamicBuffer<WavePlan> plans = entityManager.GetBuffer<WavePlan>(_run);
            WavePlan plan = plans[0];
            if (!isBoss)
            {
                plan.Duration = 100;
                plan.BossPrefab = Entity.Null;
                plan.EnemyCount = 1;
            }

            plans[0] = plan;
            for (int planIndex = 0; planIndex < waveIndex; planIndex++)
            {
                plans.Add(plan);
            }

            entityManager.GetBuffer<WaveEnemy>(_run).Add(new WaveEnemy { Prefab = _bossPrefab });
            DynamicBuffer<WaveSpawnRateCurvePoint> rates = entityManager.GetBuffer<WaveSpawnRateCurvePoint>(_run);
            rates[0] = new WaveSpawnRateCurvePoint { Rate = isBoss ? 0 : 1 };
            rates[1] = rates[0];
            WaveProgress progress = entityManager.GetComponentData<WaveProgress>(_run);
            progress.Index = waveIndex;
            entityManager.SetComponentData(_run, progress);

            Step(1);
            Step(1.5f);

            using EntityQuery enemies = entityManager.CreateEntityQuery(typeof(ArenaEnemy), typeof(CombatHealth));
            CombatHealth health = enemies.GetSingleton<CombatHealth>();
            ArenaEnemy enemy = enemies.GetSingleton<ArenaEnemy>();
            Assert.That(health.Current, Is.EqualTo(expectedHealth));
            Assert.That(health.Maximum, Is.EqualTo(expectedHealth));
            Assert.That(enemy.ContactDamage, Is.EqualTo(expectedDamage));
            Assert.That(enemy.Speed, Is.EqualTo(expectedSpeed).Within(0.0001f));
            Assert.That(entityManager.GetComponentData<CombatHealth>(_bossPrefab).Maximum, Is.EqualTo(baseHealth));
            Assert.That(entityManager.GetComponentData<ArenaEnemy>(_bossPrefab).ContactDamage, Is.EqualTo(baseDamage));
            Assert.That(entityManager.GetComponentData<ArenaEnemy>(_bossPrefab).Speed, Is.EqualTo(2.5f));
        }

        [TestCase(0, 0)]
        [TestCase(18, 18)]
        [TestCase(-18, -18)]
        public void SpawnStaysNearLivingPlayerInsideArena(float playerX, float playerZ)
        {
            EntityManager entityManager = _world.EntityManager;
            using EntityQuery players = entityManager.CreateEntityQuery(typeof(ArenaPlayer), typeof(LocalTransform));
            var playerPosition = new float2(playerX, playerZ);
            entityManager.SetComponentData(players.GetSingletonEntity(),
                LocalTransform.FromPosition(new float3(playerX, 1, playerZ)));
            Entity deadPlayer = entityManager.CreateEntity(typeof(ArenaPlayer), typeof(CombatHealth), typeof(LocalTransform));
            entityManager.SetComponentData(deadPlayer, LocalTransform.FromPosition(new float3(-15, 1, 15)));

            Step(1);

            using EntityQuery pending = entityManager.CreateEntityQuery(typeof(PendingEnemySpawn));
            float2 spawnPosition = pending.GetSingleton<PendingEnemySpawn>().Position;
            Assert.That(math.distance(spawnPosition, playerPosition), Is.InRange(7.3f, 10.3f));
            Assert.That(math.cmax(math.abs(spawnPosition)), Is.LessThanOrEqualTo(17.7f));
        }

        [Test]
        public void CoopSpawnsStayNearPlayersAndKeepClearOfBoth()
        {
            EntityManager entityManager = _world.EntityManager;
            Entity secondPlayer = entityManager.CreateEntity(typeof(ArenaPlayer), typeof(CombatHealth), typeof(LocalTransform));
            var secondPlayerPosition = new float2(10, 0);
            entityManager.SetComponentData(secondPlayer, new CombatHealth { Current = 100 });
            entityManager.SetComponentData(secondPlayer, LocalTransform.FromPosition(new float3(10, 1, 0)));
            WaveRuntimeSettings settings = entityManager.GetComponentData<WaveRuntimeSettings>(_run);
            settings.MaxAlive = 16;
            entityManager.SetComponentData(_run, settings);
            DynamicBuffer<WavePlan> plans = entityManager.GetBuffer<WavePlan>(_run);
            WavePlan plan = plans[0];
            plan.Duration = 100;
            plan.BossPrefab = Entity.Null;
            plan.EnemyCount = 1;
            plans[0] = plan;
            entityManager.GetBuffer<WaveEnemy>(_run).Add(new WaveEnemy { Prefab = _bossPrefab });
            DynamicBuffer<WaveSpawnRateCurvePoint> rates = entityManager.GetBuffer<WaveSpawnRateCurvePoint>(_run);
            rates[0] = new WaveSpawnRateCurvePoint { Rate = 16 };
            rates[1] = rates[0];

            Step(1);

            using EntityQuery pending = entityManager.CreateEntityQuery(typeof(PendingEnemySpawn));
            using NativeArray<PendingEnemySpawn> spawns = pending.ToComponentDataArray<PendingEnemySpawn>(Allocator.Temp);
            Assert.That(spawns.Length, Is.EqualTo(32));
            foreach (PendingEnemySpawn spawn in spawns)
            {
                float firstPlayerDistance = math.length(spawn.Position);
                float secondPlayerDistance = math.distance(spawn.Position, secondPlayerPosition);
                Assert.That(firstPlayerDistance, Is.GreaterThanOrEqualTo(7.3f));
                Assert.That(secondPlayerDistance, Is.GreaterThanOrEqualTo(7.3f));
                Assert.That(math.min(firstPlayerDistance, secondPlayerDistance), Is.LessThanOrEqualTo(10.3f));
            }
        }

        private void Step(float deltaTime)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + deltaTime, deltaTime));
            _spawning.Update();
            _completion.Update();
        }
    }
}
