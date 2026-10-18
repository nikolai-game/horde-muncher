using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HordeMuncher.Tests
{
    public sealed class CatalogTests
    {
        [Test]
        public void BakedCatalogPreservesWeaponThenItemOrder()
        {
            EconomySettings settings = ScriptableObject.CreateInstance<EconomySettings>();
            try
            {
                settings.Weapons = new[]
                {
                    new WeaponData
                    {
                        Name = "Pistol",
                        Damage = 23,
                        Price = 12
                    }
                };
                settings.Items = new[]
                {
                    new ItemData
                    {
                        Name = "Heart",
                        Bonus = BonusKind.Health,
                        Amount = 20,
                        Price = 8
                    }
                };
                CatalogEntry[] entries = CatalogDataBaking.Build(settings).ToArray();
                Assert.That(entries.Length, Is.EqualTo(5));
                Assert.That(entries[0].Name.ToString(), Is.EqualTo("Pistol"));
                Assert.That(entries[0].Kind, Is.EqualTo(GoodsKind.Weapon));
                Assert.That(entries[0].Damage, Is.EqualTo(23));
                Assert.That(entries[1].Name.ToString(), Is.EqualTo("Heart"));
                Assert.That(entries[1].Kind, Is.EqualTo(GoodsKind.Item));
                Assert.That(entries[1].Bonus, Is.EqualTo(BonusKind.Health));
                Assert.That(entries[1].Amount, Is.EqualTo(20));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void MigratedProjectCatalogPreservesBalanceAndModelReferences()
        {
            EconomySettings settings = AssetDatabase.LoadAssetAtPath<EconomySettings>("Assets/Project/Configs/Economy.asset");
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.Weapons.Length, Is.EqualTo(5));
            Assert.That(settings.Items.Length, Is.EqualTo(18));
            Assert.That(settings.Weapons.Select(weapon => weapon.ModelPrefab), Has.All.Not.Null);
            CatalogEntry[] entries = CatalogDataBaking.Build(settings).ToArray();
            Assert.That(entries.Take(3).Concat(entries.Skip(5).Take(8)).Select(entry => entry.Price), Is.EqualTo(new[]
            {
                18,
                24,
                24,
                24,
                22,
                22,
                20,
                18,
                18,
                18,
                28
            }));
            Assert.That(entries.Take(3).Select(entry => entry.Damage), Is.EqualTo(new[]
            {
                30,
                10,
                10
            }));
            Assert.That(entries[0].ExtraPierceTargets, Is.EqualTo(1));
            Assert.That(entries[1].KnockbackDistance, Is.EqualTo(0.25f));
            Assert.That(entries[2].RetargetOnKill, Is.False);
            Assert.That(entries[5].Bonus, Is.EqualTo(BonusKind.Health));
            Assert.That(entries[5].Amount, Is.EqualTo(6));
        }

        [Test]
        public void FirstWaveIncomeBuysTwoTypicalOrThreeCheapItemsButNeverFour()
        {
            EconomySettings economy = AssetDatabase.LoadAssetAtPath<EconomySettings>("Assets/Project/Configs/Economy.asset");
            WaveSettings waves = AssetDatabase.LoadAssetAtPath<WaveSettings>("Assets/Project/Configs/ArenaWaves.asset");
            WaveDefinition firstWave = waves.Waves[0];
            float enemyCount = 0;
            for (int curvePointIndex = 0; curvePointIndex < 128; curvePointIndex++)
            {
                enemyCount += (firstWave.SpawnRate.Evaluate(curvePointIndex / 128f)
                    + firstWave.SpawnRate.Evaluate((curvePointIndex + 1) / 128f)) * 0.5f * firstWave.Duration / 128;
            }

            int resourceReward = firstWave.EnemyPrefabs[0].GetComponent<EnemyAuthoring>().Definition.ResourceReward;
            int income = economy.StartingResources + Mathf.FloorToInt(enemyCount) * resourceReward;
            int[] itemPrices = economy.Items.Select(item => item.Price).OrderBy(price => price).ToArray();
            int typicalPrice = itemPrices[itemPrices.Length / 2];
            Assert.That(income / typicalPrice, Is.EqualTo(2));
            Assert.That(itemPrices[0] * 3, Is.LessThanOrEqualTo(income));
            Assert.That(itemPrices[0] * 4, Is.GreaterThan(income));
            Assert.That(economy.Weapons.Min(weapon => weapon.Price) * 4, Is.GreaterThan(income));
            Assert.That(economy.EnemyPriceGrowth, Is.InRange(0.1f, 0.9f),
                "Prices should grow more slowly than potential wave income.");
        }

        [Test]
        public void NewItemsPreserveBothEffectsAndMeleeTiersStayMelee()
        {
            EconomySettings settings = AssetDatabase.LoadAssetAtPath<EconomySettings>("Assets/Project/Configs/Economy.asset");
            CatalogEntry[] catalog = CatalogDataBaking.Build(settings).ToArray();
            foreach (ItemData item in settings.Items.Skip(8))
            {
                CatalogEntry entry = catalog.Single(candidate => candidate.Kind == GoodsKind.Item && candidate.Name.ToString() == item.Name);
                Assert.That(entry.Amount, Is.GreaterThan(0));
                Assert.That(entry.PenaltyAmount, Is.LessThan(0));
                Assert.That(entry.Penalty, Is.Not.EqualTo(entry.Bonus));
            }

            foreach (CatalogEntry weapon in catalog.Where(entry => WeaponCombatRules.IsMelee(entry.Weapon)))
            {
                if (weapon.UpgradeCatalogId == 0)
                {
                    continue;
                }

                CatalogEntry upgrade = catalog[weapon.UpgradeCatalogId];
                Assert.That(upgrade.Weapon, Is.EqualTo(weapon.Weapon));
                Assert.That(upgrade.Damage, Is.GreaterThan(weapon.Damage));
                Assert.That(upgrade.Range, Is.EqualTo(weapon.Range));
            }
        }

        [Test]
        public void WeaponLookupRejectsItemsAndInvalidIndices()
        {
            EconomySettings settings = ScriptableObject.CreateInstance<EconomySettings>();
            try
            {
                var weapon = new WeaponData();
                settings.Weapons = new[]
                {
                    weapon
                };
                settings.Items = new[]
                {
                    new ItemData()
                };
                Assert.That(settings.TryGetWeapon(0, out WeaponData actual), Is.True);
                Assert.That(actual, Is.SameAs(weapon));
                Assert.That(settings.TryGetWeapon(-1, out _), Is.False);
                Assert.That(settings.TryGetWeapon(1, out _), Is.False);
                Assert.That(settings.TryGetIcon(-1, out _), Is.False);
                Assert.That(settings.TryGetIcon(5, out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void EmptyCatalogEntriesFailInsteadOfShiftingIndices()
        {
            EconomySettings settings = ScriptableObject.CreateInstance<EconomySettings>();
            try
            {
                settings.Weapons = new WeaponData[]
                {
                    null
                };
                Assert.Throws<InvalidOperationException>(() => CatalogDataBaking.Build(settings).ToArray());
                settings.Weapons = Array.Empty<WeaponData>();
                settings.Items = new ItemData[]
                {
                    null
                };
                Assert.Throws<InvalidOperationException>(() => CatalogDataBaking.Build(settings).ToArray());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
    }
}
