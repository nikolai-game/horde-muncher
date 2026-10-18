using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HordeMuncher.Tests
{
    public sealed class CombatTests
    {
        [TestCase(WeaponKind.Knife, 1)]
        [TestCase(WeaponKind.Axe, 2)]
        public void MeleeStrikeHitsOnlyItsSectorWithoutProjectilesAndRespectsCooldown(WeaponKind kind, int targets)
        {
            using var arena = new CombatTestArena();
            Entity player = arena.CreatePlayer(0);
            arena.EntityManager.AddComponentData(player, new PlayerBonuses
            {
                MeleeDamage = 4,
                RangedDamage = 100,
                Damage = 0.5f,
                CriticalChance = 1,
                CriticalDamageMultiplier = 2
            });
            Entity configuration = arena.EntityManager.CreateEntity(typeof(EconomyConfig));
            arena.EntityManager.AddBuffer<CatalogEntry>(configuration).Add(new CatalogEntry
            {
                Kind = GoodsKind.Weapon,
                Weapon = kind,
                Damage = 10,
                Interval = 1,
                Range = 2.5f,
                Targets = targets,
                Cone = 90
            });
            arena.EntityManager.AddBuffer<EquippedWeapon>(player).Add(new EquippedWeapon { CatalogId = 0 });
            Entity near = arena.CreateEnemy(1.5f, 100);
            Entity inSector = arena.CreateEnemy(2, 100);
            Entity behind = arena.CreateEnemy(-2, 100);
            Entity outsideRange = arena.CreateEnemy(3, 100);
            Entity outsideSector = arena.CreateEnemy(0, 100);
            arena.EntityManager.SetComponentData(outsideSector, LocalTransform.FromPosition(0, 0, 2));
            arena.Step();

            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(near).Current, Is.EqualTo(58));
            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(inSector).Current, Is.EqualTo(targets == 2 ? 58 : 100));
            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(behind).Current, Is.EqualTo(100));
            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(outsideRange).Current, Is.EqualTo(100));
            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(outsideSector).Current, Is.EqualTo(100));
            using EntityQuery projectiles = arena.EntityManager.CreateEntityQuery(typeof(ArenaProjectile));
            Assert.That(projectiles.CalculateEntityCount(), Is.Zero);
            arena.Step();
            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(near).Current, Is.EqualTo(58));
        }

        [TestCase(WeaponKind.Knife, 0.5f, 2.8f, 70)]
        [TestCase(WeaponKind.Axe, 0.5f, 2.8f, 70)]
        [TestCase(WeaponKind.Knife, 0, 2.8f, 100)]
        [TestCase(WeaponKind.Axe, 0, 2.8f, 100)]
        [TestCase(WeaponKind.Knife, -0.5f, 1.8f, 100)]
        [TestCase(WeaponKind.Axe, -0.5f, 1.8f, 100)]
        public void RangeBonusChangesMeleeTargetAcquisitionAndStrikeReach(WeaponKind kind, float rangeBonus, float targetDistance, int expectedHealth)
        {
            using var arena = new CombatTestArena();
            Entity player = arena.CreatePlayer(0);
            arena.EntityManager.AddComponentData(player, new PlayerBonuses { Range = rangeBonus });
            Entity configuration = arena.EntityManager.CreateEntity(typeof(EconomyConfig));
            arena.EntityManager.AddBuffer<CatalogEntry>(configuration).Add(new CatalogEntry
            {
                Kind = GoodsKind.Weapon,
                Weapon = kind,
                Damage = 30,
                Interval = 1,
                Range = 2.5f,
                Targets = 2,
                Cone = 90
            });
            arena.EntityManager.AddBuffer<EquippedWeapon>(player).Add(new EquippedWeapon { CatalogId = 0 });
            Entity target = arena.CreateEnemy(targetDistance, 100);
            Entity secondTarget = arena.CreateEnemy(3.5f, 100);
            Entity outsideBonusRange = arena.CreateEnemy(4, 100);
            arena.Step();

            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(target).Current, Is.EqualTo(expectedHealth));
            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(secondTarget).Current, Is.EqualTo(expectedHealth));
            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(outsideBonusRange).Current, Is.EqualTo(100));
        }

        [Test]
        public void TradeoffStacksBothEffectsOnceAndHealthPenaltyCannotKillPlayer()
        {
            using var arena = new CombatTestArena();
            Entity player = arena.CreatePlayer(0);
            arena.EntityManager.AddComponentData(player, new PlayerBonuses());
            arena.EntityManager.AddBuffer<OwnedItem>(player);
            var item = new CatalogEntry
            {
                Bonus = BonusKind.MeleeDamage,
                Amount = 6,
                Penalty = BonusKind.Health,
                PenaltyAmount = -60
            };
            EconomyRules.ApplyItemBonus(arena.EntityManager, player, 7, item);
            EconomyRules.ApplyItemBonus(arena.EntityManager, player, 7, item);
            Assert.That(arena.EntityManager.GetComponentData<PlayerBonuses>(player).MeleeDamage, Is.EqualTo(12));
            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(player).Maximum, Is.EqualTo(1));
            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(player).Current, Is.EqualTo(1));
            DynamicBuffer<OwnedItem> inventory = arena.EntityManager.GetBuffer<OwnedItem>(player);
            Assert.That(inventory.Length, Is.EqualTo(1));
            Assert.That(inventory[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void RangedDamageUsesOnlyRangedBonusAndNegativeBonusesCannotReverseDamage()
        {
            var weapon = new CatalogEntry { Weapon = WeaponKind.Pistol, Damage = 10 };
            var bonuses = new PlayerBonuses { RangedDamage = 4, MeleeDamage = 100, Damage = 0.5f };
            Assert.That(WeaponCombatRules.CalculateDamage(weapon, bonuses, false), Is.EqualTo(21));
            bonuses.RangedDamage = -100;
            bonuses.Damage = -2;
            Assert.That(WeaponCombatRules.CalculateDamage(weapon, bonuses, false), Is.EqualTo(1));
        }

        [TestCase(0, 15, false)]
        [TestCase(1, 45, true)]
        public void PlayerCriticalStatsDetermineProjectileDamageAndFloatingNumber(float criticalChance, int expectedDamage, bool expectedCritical)
        {
            using var arena = new CombatTestArena();
            Entity player = arena.CreatePlayer(0, 10);
            Entity enemy = arena.CreateEnemy(3, 1000);
            arena.EntityManager.AddComponentData(player, new PlayerBonuses
            {
                Damage = 0.5f,
                CriticalChance = criticalChance,
                CriticalDamageMultiplier = 3
            });
            arena.Step();
            arena.Step();

            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(enemy).Current, Is.EqualTo(1000 - expectedDamage));
            using EntityQuery query = arena.EntityManager.CreateEntityQuery(typeof(CombatHealthChangeRpc));
            using NativeArray<CombatHealthChangeRpc> changes = query.ToComponentDataArray<CombatHealthChangeRpc>(Allocator.Temp);
            Assert.That(changes.Length, Is.EqualTo(1));
            Assert.That(changes[0].HealthChange, Is.EqualTo(-expectedDamage));
            Assert.That(changes[0].IsCritical, Is.EqualTo(expectedCritical));
        }

        [Test]
        public void LethalHitKeepsDamageNumberAfterEnemyIsDestroyed()
        {
            using var arena = new CombatTestArena();
            arena.CreatePlayer(0, 50);
            Entity enemy = arena.CreateEnemy(3, 20);
            arena.Step();
            arena.Step();

            Assert.That(arena.EntityManager.Exists(enemy), Is.False);
            using EntityQuery query = arena.EntityManager.CreateEntityQuery(typeof(CombatHealthChangeRpc));
            using NativeArray<CombatHealthChangeRpc> changes = query.ToComponentDataArray<CombatHealthChangeRpc>(Allocator.Temp);
            Assert.That(changes.Length, Is.EqualTo(1));
            Assert.That(changes[0].IsPlayer, Is.False);
            Assert.That(changes[0].HealthChange, Is.EqualTo(-20));
            Assert.That(changes[0].Position.x, Is.EqualTo(3));
        }

        [Test]
        public void DamageAndHealingInSameStepProduceSeparateNumbersAndFullHealthProducesNone()
        {
            using var arena = new CombatTestArena();
            Entity player = arena.CreatePlayer(0, 1);
            Entity enemy = arena.CreateEnemy(0.8f, 1000);
            arena.EntityManager.AddComponentData(player, new PlayerBonuses { Regeneration = 50 });
            arena.Step();

            using EntityQuery query = arena.EntityManager.CreateEntityQuery(typeof(CombatHealthChangeRpc));
            using NativeArray<CombatHealthChangeRpc> changes = query.ToComponentDataArray<CombatHealthChangeRpc>(Allocator.Temp);
            Assert.That(changes.Length, Is.EqualTo(2));
            int totalChange = 0;
            foreach (CombatHealthChangeRpc change in changes)
            {
                Assert.That(change.IsPlayer, Is.True);
                Assert.That(change.HealthChange, Is.EqualTo(-10).Or.EqualTo(5));
                totalChange += change.HealthChange;
            }

            Assert.That(totalChange, Is.EqualTo(-5));
            arena.EntityManager.DestroyEntity(query);
            arena.EntityManager.DestroyEntity(enemy);
            CombatHealth health = arena.EntityManager.GetComponentData<CombatHealth>(player);
            health.Current = health.Maximum;
            arena.EntityManager.SetComponentData(player, health);
            arena.Step();
            Assert.That(query.CalculateEntityCount(), Is.Zero);
        }

        [Test]
        public void WeaponTargetsNearestEnemyAndRespectsCooldown()
        {
            using var arena = new CombatTestArena();
            Entity player = arena.CreatePlayer(0);
            Entity near = arena.CreateEnemy(3);
            Entity farEnemy = arena.CreateEnemy(5);
            arena.Step();
            arena.Step();
            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(near).Current, Is.EqualTo(15));
            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(farEnemy).Current, Is.EqualTo(30));
            arena.Step();
            Assert.That(arena.EntityManager.GetComponentData<WeaponShot>(player).Sequence, Is.EqualTo(1));
            arena.Step(0.2f);
            arena.Step();
            Assert.That(arena.EntityManager.Exists(near), Is.False);
            Assert.That(arena.Count<ResourcePickup>(), Is.EqualTo(1));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(5)]
        public void EnemyDropsSeparateUnitPickupsAtDistinctPositions(int resourceReward)
        {
            using var arena = new CombatTestArena();
            EntityManager entityManager = arena.EntityManager;
            Entity enemy = arena.CreateEnemy(5);
            ArenaEnemy enemyData = entityManager.GetComponentData<ArenaEnemy>(enemy);
            enemyData.ResourceReward = resourceReward;
            entityManager.SetComponentData(enemy, enemyData);
            entityManager.AddComponentData(enemy, new EnemyDeath());
            arena.Step();

            Assert.That(entityManager.Exists(enemy), Is.False);
            using EntityQuery pickupQuery = entityManager.CreateEntityQuery(typeof(ResourcePickup), typeof(LocalTransform));
            using NativeArray<ResourcePickup> pickups = pickupQuery.ToComponentDataArray<ResourcePickup>(Allocator.Temp);
            using NativeArray<LocalTransform> transforms = pickupQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            Assert.That(pickups.Length, Is.EqualTo(resourceReward));
            for (int pickupIndex = 0; pickupIndex < pickups.Length; pickupIndex++)
            {
                Assert.That(pickups[pickupIndex].Value, Is.EqualTo(1));
                Assert.That(pickups[pickupIndex].IsItem, Is.False);
                Assert.That(transforms[pickupIndex].Position.y, Is.EqualTo(0.25f));
                for (int previousIndex = 0; previousIndex < pickupIndex; previousIndex++)
                {
                    Assert.That(transforms[pickupIndex].Position, Is.Not.EqualTo(transforms[previousIndex].Position));
                }
            }

            arena.Step();
            Assert.That(arena.Count<ResourcePickup>(), Is.EqualTo(resourceReward));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(5)]
        public void SimultaneousHitsAwardOneKillAndCollectFullEnemyReward(int resourceReward)
        {
            using var arena = new CombatTestArena();
            Entity firstPlayer = arena.CreatePlayer(0, 30);
            Entity secondPlayer = arena.CreatePlayer(0, 30);
            Entity enemy = arena.CreateEnemy(0.4f);
            ArenaEnemy enemyData = arena.EntityManager.GetComponentData<ArenaEnemy>(enemy);
            enemyData.ResourceReward = resourceReward;
            arena.EntityManager.SetComponentData(enemy, enemyData);
            arena.Step();
            arena.Step();
            arena.Step(0.5f);
            CombatWallet firstWallet = arena.EntityManager.GetComponentData<CombatWallet>(firstPlayer);
            CombatWallet secondWallet = arena.EntityManager.GetComponentData<CombatWallet>(secondPlayer);
            Assert.That(firstWallet.Kills + secondWallet.Kills, Is.EqualTo(1));
            Assert.That(firstWallet.Resources + secondWallet.Resources, Is.EqualTo(resourceReward));
            Assert.That(arena.Count<ResourcePickup>(), Is.Zero);
        }

        [TestCase(0)]
        [TestCase(20)]
        public void PickupsImmediatelyCollectAndAlternateWalletRecipients(float pickupPosition)
        {
            using var arena = new CombatTestArena();
            Entity firstPlayer = arena.CreatePlayer(0);
            Entity secondPlayer = arena.CreatePlayer(20);
            for (int pickupIndex = 0; pickupIndex < 4; pickupIndex++)
            {
                Entity resource = arena.EntityManager.CreateEntity(typeof(ResourcePickup), typeof(LocalTransform));
                arena.EntityManager.SetComponentData(resource, new ResourcePickup { Value = 1 });
                arena.EntityManager.SetComponentData(resource, LocalTransform.FromPosition(pickupPosition + 0.5f, 0, 0));
                arena.Step();

                Assert.That(arena.EntityManager.Exists(resource), Is.False);
                Assert.That(arena.EntityManager.GetComponentData<CombatWallet>(firstPlayer).Resources, Is.EqualTo((pickupIndex + 2) / 2));
                Assert.That(arena.EntityManager.GetComponentData<CombatWallet>(secondPlayer).Resources, Is.EqualTo((pickupIndex + 1) / 2));
            }
        }

        [Test]
        public void SimultaneousPickupsSkipDefeatedPlayers()
        {
            using var arena = new CombatTestArena();
            Entity firstPlayer = arena.CreatePlayer(0);
            arena.CreatePlayer(10, health: 0);
            Entity lastPlayer = arena.CreatePlayer(20, health: 0);
            for (int pickupIndex = 0; pickupIndex < 4; pickupIndex++)
            {
                Entity resource = arena.EntityManager.CreateEntity(typeof(ResourcePickup), typeof(LocalTransform));
                arena.EntityManager.SetComponentData(resource, new ResourcePickup { Value = 1 });
                arena.EntityManager.SetComponentData(resource, LocalTransform.FromPosition(0, 0, 0));
            }

            arena.Step();

            Assert.That(arena.EntityManager.GetComponentData<CombatWallet>(firstPlayer).Resources, Is.EqualTo(4));
            Assert.That(arena.EntityManager.GetComponentData<CombatWallet>(lastPlayer).Resources, Is.Zero);
            Assert.That(arena.Count<ResourcePickup>(), Is.Zero);
        }

        [Test]
        public void ResourceInPickupRadiusFollowsConfiguredAttractionCurveBeforeCollection()
        {
            using var arena = new CombatTestArena();
            Entity player = arena.CreatePlayer(0);
            Entity configuration = arena.EntityManager.CreateEntity(typeof(EconomyConfig));
            arena.EntityManager.SetComponentData(configuration, new EconomyConfig { ResourceAttractionDuration = 1 });
            DynamicBuffer<ResourceAttractionCurvePoint> attractionCurvePoints =
                arena.EntityManager.AddBuffer<ResourceAttractionCurvePoint>(configuration);
            attractionCurvePoints.Add(new ResourceAttractionCurvePoint { Progress = 0 });
            attractionCurvePoints.Add(new ResourceAttractionCurvePoint { Progress = 0.25f });
            attractionCurvePoints.Add(new ResourceAttractionCurvePoint { Progress = 1 });
            Entity resource = arena.EntityManager.CreateEntity(typeof(ResourcePickup), typeof(LocalTransform));
            arena.EntityManager.SetComponentData(resource, new ResourcePickup { Value = 3 });
            arena.EntityManager.SetComponentData(resource,
                LocalTransform.FromPositionRotationScale(new float3(1.2f, 0, 0), quaternion.identity, 0.45f));
            arena.Step();

            Assert.That(arena.EntityManager.Exists(resource), Is.True);
            Assert.That(arena.EntityManager.GetComponentData<ResourcePickup>(resource).Collector, Is.EqualTo(player));
            Assert.That(arena.EntityManager.GetComponentData<CombatWallet>(player).Resources, Is.Zero);

            arena.Step(0.5f);

            Assert.That(arena.EntityManager.GetComponentData<LocalTransform>(resource).Position.x, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(arena.EntityManager.GetComponentData<CombatWallet>(player).Resources, Is.Zero);

            arena.Step(0.5f);

            Assert.That(arena.EntityManager.Exists(resource), Is.False);
            Assert.That(arena.EntityManager.GetComponentData<CombatWallet>(player).Resources, Is.EqualTo(3));
        }

        [TestCase(0.724f, true, false)]
        [TestCase(0.726f, false, true)]
        [TestCase(1.4f, false, true)]
        [TestCase(1.401f, false, false)]
        public void ResourceCollectsOnContactAndAttractsOnlyInsidePickupRadius(float distance, bool collected, bool attracted)
        {
            using var arena = new CombatTestArena();
            Entity player = arena.CreatePlayer(0);
            Entity resource = arena.EntityManager.CreateEntity(typeof(ResourcePickup), typeof(LocalTransform));
            arena.EntityManager.SetComponentData(resource, new ResourcePickup { Value = 1 });
            arena.EntityManager.SetComponentData(resource,
                LocalTransform.FromPositionRotationScale(new float3(distance, 0, 0), quaternion.identity, 0.45f));

            arena.Step();

            Assert.That(arena.EntityManager.GetComponentData<CombatWallet>(player).Resources, Is.EqualTo(collected ? 1 : 0));
            Assert.That(arena.EntityManager.Exists(resource), Is.EqualTo(!collected));
            if (!collected)
            {
                Assert.That(arena.EntityManager.GetComponentData<ResourcePickup>(resource).Collector,
                    Is.EqualTo(attracted ? player : Entity.Null));
            }
        }

        [Test]
        public void TouchingAttractedResourceCollectsWithoutWaitingForAnimation()
        {
            using var arena = new CombatTestArena();
            Entity player = arena.CreatePlayer(0);
            Entity resource = arena.EntityManager.CreateEntity(typeof(ResourcePickup), typeof(LocalTransform));
            arena.EntityManager.SetComponentData(resource, new ResourcePickup { Value = 1 });
            arena.EntityManager.SetComponentData(resource,
                LocalTransform.FromPositionRotationScale(new float3(1.2f, 0, 0), quaternion.identity, 0.45f));
            arena.Step();

            arena.EntityManager.SetComponentData(player, LocalTransform.FromPosition(0.6f, 1, 0));
            arena.Step(0.01f);

            Assert.That(arena.EntityManager.Exists(resource), Is.False);
            Assert.That(arena.EntityManager.GetComponentData<CombatWallet>(player).Resources, Is.EqualTo(1));
        }

        [TestCase(0, 2, 2, 0)]
        [TestCase(5, 2, 4, 3)]
        [TestCase(1, 2, 3, 0)]
        public void PickupConsumesOnlyAvailableBagReserve(int reserve, int pickupValue, int expectedResources, int expectedReserve)
        {
            using var arena = new CombatTestArena();
            Entity player = arena.CreatePlayer(0);
            arena.EntityManager.SetComponentData(player, new CombatWallet { BankedResources = reserve });
            Entity resource = arena.EntityManager.CreateEntity(typeof(ResourcePickup), typeof(LocalTransform));
            arena.EntityManager.SetComponentData(resource, new ResourcePickup { Value = pickupValue });
            arena.EntityManager.SetComponentData(resource, LocalTransform.FromPosition(0, 0, 0));
            arena.Step();
            arena.Step(0.5f);
            CombatWallet wallet = arena.EntityManager.GetComponentData<CombatWallet>(player);
            Assert.That(wallet.Resources, Is.EqualTo(expectedResources));
            Assert.That(wallet.BankedResources, Is.EqualTo(expectedReserve));
            Assert.That(wallet.BonusPickupSequence, Is.EqualTo(reserve > 0 ? 1 : 0));
        }

        [Test]
        public void WaveRemainderFliesIntoBagThenDoublesPickupsWithoutCreatingExtraMaterials()
        {
            using var arena = new CombatTestArena();
            Entity player = arena.CreatePlayer(0);
            Entity run = arena.EntityManager.CreateEntity(typeof(WaveProgress));
            arena.EntityManager.SetComponentData(run, new WaveProgress { Phase = WavePhase.WaveCleared });
            Entity remainder = arena.EntityManager.CreateEntity(typeof(ResourcePickup), typeof(LocalTransform));
            arena.EntityManager.SetComponentData(remainder, new ResourcePickup { Value = 3 });
            arena.EntityManager.SetComponentData(remainder, LocalTransform.FromPosition(10, 0, 0));
            ArenaRunRules.CompleteWave(arena.EntityManager);
            arena.Step(1);
            Assert.That(arena.EntityManager.Exists(remainder), Is.True);
            Assert.That(arena.EntityManager.GetComponentData<LocalTransform>(remainder).Position.x, Is.InRange(0.01f, 9.99f));
            Assert.That(arena.EntityManager.GetComponentData<CombatWallet>(player).Resources, Is.Zero);
            arena.Step(1);
            Assert.That(arena.EntityManager.Exists(remainder), Is.False);
            Assert.That(arena.EntityManager.GetComponentData<CombatWallet>(player).BankedResources, Is.EqualTo(3));
            arena.EntityManager.SetComponentData(run, new WaveProgress { Phase = WavePhase.Spawning });
            Entity pickup = arena.EntityManager.CreateEntity(typeof(ResourcePickup), typeof(LocalTransform));
            arena.EntityManager.SetComponentData(pickup, new ResourcePickup { Value = 5 });
            arena.EntityManager.SetComponentData(pickup, LocalTransform.FromPosition(0, 0, 0));
            arena.Step();
            arena.Step(0.5f);
            CombatWallet wallet = arena.EntityManager.GetComponentData<CombatWallet>(player);
            Assert.That(wallet.Resources, Is.EqualTo(8));
            Assert.That(wallet.BankedResources, Is.Zero);
            Assert.That(wallet.BonusPickupSequence, Is.EqualTo(1));
        }

        [Test]
        public void WeaponDoesNotFireOutsideRange()
        {
            using var arena = new CombatTestArena();
            Entity player = arena.CreatePlayer(0);
            arena.CreateEnemy(12);
            arena.Step();
            Assert.That(arena.EntityManager.GetComponentData<WeaponShot>(player).Sequence, Is.Zero);
        }

        [TestCase(0.5f, 1f, 90)]
        [TestCase(0.225f, 0.724f, 90)]
        [TestCase(0.225f, 0.726f, 100)]
        [TestCase(0.325f, 0.824f, 90)]
        [TestCase(0.325f, 0.826f, 100)]
        [TestCase(0.55f, 1.049f, 90)]
        [TestCase(0.55f, 1.051f, 100)]
        [TestCase(1.3f, 1.799f, 90)]
        [TestCase(1.3f, 1.801f, 100)]
        public void EnemyDealsDamageOnlyWhenBodiesTouch(float enemyRadius, float centerDistance, int expectedHealth)
        {
            using var arena = new CombatTestArena();
            Entity player = arena.CreatePlayer(0, 1);
            Entity enemy = arena.CreateEnemy(centerDistance, 1000);
            ArenaEnemy enemyData = arena.EntityManager.GetComponentData<ArenaEnemy>(enemy);
            enemyData.Radius = enemyRadius;
            arena.EntityManager.SetComponentData(enemy, enemyData);

            arena.Step();

            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(player).Current, Is.EqualTo(expectedHealth));
        }

        [Test]
        public void PassingBetweenEnemiesWithoutTouchingDoesNotDealDamage()
        {
            using var arena = new CombatTestArena();
            Entity player = arena.CreatePlayer(0, 1);
            arena.CreateEnemy(-0.9f, 1000);
            arena.CreateEnemy(0.9f, 1000);

            for (int step = -10; step <= 10; step++)
            {
                arena.EntityManager.SetComponentData(player, LocalTransform.FromPosition(0, 1, step * 0.2f));
                arena.Step();
            }

            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(player).Current, Is.EqualTo(100));
        }

        [Test]
        public void ContactInvulnerabilityPreventsStackedDamageAndDeadPlayersCannotFire()
        {
            using var arena = new CombatTestArena();
            Entity player = arena.CreatePlayer(0, 1, 20);
            arena.CreateEnemy(0.8f, 1000);
            arena.CreateEnemy(0.9f, 1000);
            arena.Step();
            arena.Step();
            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(player).Current, Is.EqualTo(10));
            arena.Step(0.8f);
            Assert.That(arena.EntityManager.GetComponentData<CombatHealth>(player).Current, Is.Zero);
            uint shots = arena.EntityManager.GetComponentData<WeaponShot>(player).Sequence;
            arena.Step(1);
            Assert.That(arena.EntityManager.GetComponentData<WeaponShot>(player).Sequence, Is.EqualTo(shots));
        }
    }
}
