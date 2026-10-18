using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HordeMuncher.Tests
{
    public sealed class ItemDropTests
    {
        [TestCase(-10, 0)]
        [TestCase(0, 0)]
        [TestCase(20, 0.02f)]
        [TestCase(100, 0.1f)]
        [TestCase(1000, 0.25f)]
        public void DropChanceRequiresPositiveLuckAndHasACap(int luck, float chance)
        {
            Assert.That(ItemDropRules.CalculateDropChance(luck), Is.EqualTo(chance).Within(0.00001f));
        }

        [Test]
        public void DropsUseOnlyItemsAndNeverWeapons()
        {
            using var world = new World("Item drop tests");
            Entity config = world.EntityManager.CreateEntity();
            DynamicBuffer<CatalogEntry> catalog = world.EntityManager.AddBuffer<CatalogEntry>(config);
            catalog.Add(new CatalogEntry
            {
                Kind = GoodsKind.Weapon,
                Weight = 1000
            });
            catalog.Add(new CatalogEntry
            {
                Kind = GoodsKind.Item,
                Weight = 1
            });
            var random = new Random(123);
            int drops = 0;
            for (int roll = 0; roll < 200; roll++)
            {
                if (ItemDropRules.TryRollItem(catalog, 250, ref random, out int catalogId))
                {
                    Assert.That(catalogId, Is.EqualTo(1));
                    drops++;
                }

                Assert.That(ItemDropRules.TryRollItem(catalog, 0, ref random, out _), Is.False);
            }

            Assert.That(drops, Is.GreaterThan(0));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CollectingItemQueuesRewardWithoutApplyingBonusOrAwardingCurrency(bool storeInBag)
        {
            using var arena = new CombatTestArena();
            EntityManager manager = arena.EntityManager;
            Entity player = arena.CreatePlayer(0);
            manager.AddComponentData(player, new PlayerProgress());
            manager.AddComponentData(player, new PlayerBonuses());
            manager.AddBuffer<OwnedItem>(player);
            manager.AddBuffer<PendingItemReward>(player);
            Entity config = manager.CreateEntity(typeof(EconomyConfig));
            manager.AddBuffer<CatalogEntry>(config).Add(new CatalogEntry
            {
                Kind = GoodsKind.Item,
                Bonus = BonusKind.Luck,
                Amount = 5
            });
            Entity item = manager.CreateEntity(typeof(ResourcePickup), typeof(LocalTransform));
            manager.SetComponentData(item, LocalTransform.Identity);
            manager.SetComponentData(item, new ResourcePickup
            {
                IsItem = true,
                ItemCatalogId = 0,
                Collector = player,
                StoreInBag = storeInBag
            });
            arena.Step(3);
            arena.Step();
            Assert.That(manager.Exists(item), Is.False);
            Assert.That(manager.GetComponentData<PlayerBonuses>(player).Luck, Is.Zero);
            Assert.That(manager.GetBuffer<OwnedItem>(player).Length, Is.Zero);
            Assert.That(manager.GetBuffer<PendingItemReward>(player).Length, Is.EqualTo(1));
            Assert.That(manager.GetComponentData<PlayerProgress>(player).CollectedItems, Is.EqualTo(1));
            Assert.That(manager.GetComponentData<CombatWallet>(player).Resources, Is.Zero);
            Assert.That(manager.GetComponentData<CombatWallet>(player).BankedResources, Is.Zero);
        }
    }
}
