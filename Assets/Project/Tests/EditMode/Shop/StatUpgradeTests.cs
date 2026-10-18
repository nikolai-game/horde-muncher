using System.Linq;
using HordeMuncher.UI.Contracts;
using HordeMuncher.UI.Shop.Presentation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HordeMuncher.Tests.Shop
{
    public sealed class StatUpgradeTests
    {
        [TestCase(BonusKind.MeleeDamage, 1)]
        [TestCase(BonusKind.RangedDamage, 1)]
        [TestCase(BonusKind.Damage, 3)]
        [TestCase(BonusKind.AttackSpeed, 3)]
        [TestCase(BonusKind.MoveSpeed, 2)]
        [TestCase(BonusKind.Regeneration, 1)]
        [TestCase(BonusKind.CriticalDamageMultiplier, 5)]
        public void LevelUpBonusesKeepEvenLegendaryGainsGradual(BonusKind stat, int commonPoints)
        {
            using var arena = new ShopBridgeTestArena(isServer: true);
            var progress = new PlayerProgress { PendingUpgrades = 1, RandomState = 17 };
            bool foundStat = false;
            for (int generation = 0; generation < 100; generation++)
            {
                EconomyRules.GenerateUpgradeChoices(arena.World.EntityManager, arena.Player, ref progress);
                foreach (UpgradeChoice choice in arena.World.EntityManager.GetBuffer<UpgradeChoice>(arena.Player))
                {
                    if (choice.Stat != stat)
                    {
                        continue;
                    }

                    foundStat = true;
                    Assert.That(choice.Points, Is.InRange(commonPoints, commonPoints * 4));
                    if (choice.Rarity == StatUpgradeRarity.Common)
                    {
                        Assert.That(choice.Points, Is.EqualTo(commonPoints));
                    }
                }
            }

            Assert.That(foundStat, Is.True);
        }

        [Test]
        public void EmptyItemCatalogStillOffersFourDistinctStatsAndExcludesCappedCriticalChance()
        {
            using var arena = new ShopBridgeTestArena(isServer: true);
            EntityManager manager = arena.World.EntityManager;
            manager.GetBuffer<CatalogEntry>(arena.Config).Clear();
            manager.SetComponentData(arena.Player, new PlayerBonuses { CriticalChance = 1 });
            var progress = new PlayerProgress { PendingUpgrades = 1, RandomState = 17 };
            for (int generation = 0; generation < 100; generation++)
            {
                EconomyRules.GenerateUpgradeChoices(manager, arena.Player, ref progress);
                using var nativeChoices = manager.GetBuffer<UpgradeChoice>(arena.Player).ToNativeArray(Unity.Collections.Allocator.Temp);
                UpgradeChoice[] choices = nativeChoices.ToArray();
                Assert.That(choices.Length, Is.EqualTo(4));
                Assert.That(choices.Select(choice => choice.Stat).Distinct().Count(), Is.EqualTo(4));
                Assert.That(choices.Any(choice => choice.Stat == BonusKind.CriticalChance), Is.False);
                foreach (UpgradeChoice choice in choices)
                {
                    Assert.That(choice.Points, Is.EqualTo(StatUpgradeRules.GetBasePoints(choice.Stat) * ((int)choice.Rarity + 1)));
                }
            }
        }

        [Test]
        public void ChoosingLuckAddsPointsWithoutItemsOrPaymentAndGeneratesTheNextLevel()
        {
            using var arena = new ShopBridgeTestArena(isServer: true);
            EntityManager manager = arena.World.EntityManager;
            var progress = new PlayerProgress { PendingUpgrades = 2, RandomState = 17 };
            manager.SetComponentData(arena.Player, progress);
            DynamicBuffer<UpgradeChoice> choices = manager.GetBuffer<UpgradeChoice>(arena.Player);
            choices[0] = new UpgradeChoice
            {
                Stat = BonusKind.Luck,
                Rarity = StatUpgradeRarity.Legendary,
                Points = 20
            };
            int itemCount = manager.GetBuffer<OwnedItem>(arena.Player).Length;
            int resources = manager.GetComponentData<CombatWallet>(arena.Player).Resources;
            ShopResult result = EconomyRules.ApplyShopRequest(manager, arena.Player,
                new ShopRequest { Action = ShopAction.Upgrade, Slot = 0 },
                manager.GetComponentData<EconomyConfig>(arena.Config), manager.GetBuffer<CatalogEntry>(arena.Config));
            Assert.That(result, Is.EqualTo(ShopResult.Success));
            Assert.That(manager.GetComponentData<PlayerBonuses>(arena.Player).Luck, Is.EqualTo(20));
            Assert.That(manager.GetBuffer<OwnedItem>(arena.Player).Length, Is.EqualTo(itemCount));
            Assert.That(manager.GetComponentData<CombatWallet>(arena.Player).Resources, Is.EqualTo(resources));
            Assert.That(manager.GetComponentData<PlayerProgress>(arena.Player).PendingUpgrades, Is.EqualTo(1));
            Assert.That(manager.GetBuffer<UpgradeChoice>(arena.Player).Length, Is.EqualTo(4));
        }

        [Test]
        public void LuckIncreasesRarityAndNegativeLuckUsesTheBaseline()
        {
            int baselineTotal = 0;
            int luckyTotal = 0;
            for (uint seed = 1; seed <= 10000; seed++)
            {
                var baselineRandom = new Random(seed);
                var luckyRandom = new Random(seed);
                var negativeRandom = new Random(seed);
                StatUpgradeRarity baseline = StatUpgradeRules.RollRarity(0, ref baselineRandom);
                StatUpgradeRarity lucky = StatUpgradeRules.RollRarity(100, ref luckyRandom);
                Assert.That((int)lucky, Is.GreaterThanOrEqualTo((int)baseline));
                Assert.That(StatUpgradeRules.RollRarity(-100, ref negativeRandom), Is.EqualTo(baseline));
                baselineTotal += (int)baseline;
                luckyTotal += (int)lucky;
            }

            Assert.That(luckyTotal, Is.GreaterThan(baselineTotal));
        }

        [Test]
        public void UpgradeCardsUsePointsWithoutExplanationsAndStatRowsKeepInformation()
        {
            ShopState state = FakeShopModel.CreateState();
            state.PendingUpgrades = 1;
            state.Stats.Regeneration = 0.4f;
            state.UpgradeChoices = new[] { new ShopUpgradeChoice { Stat = ShopBonusKind.Regeneration, Points = 2 } };
            ShopViewData data = ShopViewDataBuilder.Build(state, 0, false, "");
            Assert.That(data.Offers[0].Name, Is.EqualTo("Регенерация +2"));
            Assert.That(data.Offers[0].Description, Is.Null.Or.Empty);
            Assert.That(data.Offers[0].Tooltip, Is.Empty);
            Assert.That(data.Stats.All(stat => !stat.Value.Contains("%") && !stat.Value.Contains(" м") && !stat.Value.Contains("HP")), Is.True);
            Assert.That(data.Stats.Single(stat => stat.Name == "Удача").Tooltip, Does.Contain("следующий набор"));
        }
    }
}
