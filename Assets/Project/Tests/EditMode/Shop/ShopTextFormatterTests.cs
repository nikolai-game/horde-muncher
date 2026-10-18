using HordeMuncher.UI.Contracts;
using HordeMuncher.UI.Shop.Presentation;
using NUnit.Framework;

namespace HordeMuncher.Tests.Shop
{
    public sealed class ShopTextFormatterTests
    {
        [Test]
        public void ItemDescriptionUsesCurrentBonusesInsteadOfLegacyText()
        {
            var item = new ShopProduct
            {
                Description = "Старое описание: +1 HP/с",
                Bonus = ShopBonusKind.Regeneration,
                Amount = 2,
                Penalty = ShopBonusKind.Range,
                PenaltyAmount = -0.15f
            };
            string description = ShopTextFormatter.FormatProductDescription(item, new ShopStats());
            Assert.That(description, Is.EqualTo("+20 Регенерация\n−15 Дальность"));
        }

        [TestCase(ShopBonusKind.Health, 20, "+20 Здоровье")]
        [TestCase(ShopBonusKind.Damage, 0.1f, "+10 Урон")]
        [TestCase(ShopBonusKind.AttackSpeed, 0.1f, "+10 Скорость атаки")]
        [TestCase(ShopBonusKind.MoveSpeed, -0.08f, "−8 Скорость движения")]
        [TestCase(ShopBonusKind.Range, 0.15f, "+15 Дальность")]
        [TestCase(ShopBonusKind.Pickup, 0.75f, "+15 Радиус подбора")]
        [TestCase(ShopBonusKind.Armor, 1, "+1 Броня")]
        [TestCase(ShopBonusKind.Regeneration, 1, "+10 Регенерация")]
        [TestCase(ShopBonusKind.CriticalChance, 0.12f, "+12 Шанс крита")]
        [TestCase(ShopBonusKind.CriticalDamageMultiplier, 0.25f, "+25 Критический урон")]
        [TestCase(ShopBonusKind.MeleeDamage, 4, "+4 Ближний бой")]
        [TestCase(ShopBonusKind.RangedDamage, -2, "−2 Дальний бой")]
        [TestCase(ShopBonusKind.Luck, 5, "+5 Удача")]
        public void ItemStatChangeShowsPointsWithoutEffectUnits(ShopBonusKind stat, float amount, string expectedText)
        {
            Assert.That(ShopTextFormatter.FormatItemStatChange(stat, amount), Is.EqualTo(expectedText));
        }

        [TestCase(true, "21")]
        [TestCase(false, "45")]
        public void WeaponPreviewUsesMatchingCombatStat(bool isMelee, string expectedDamage)
        {
            var weapon = new ShopProduct { IsWeapon = true, IsMelee = isMelee, Damage = 10 };
            var stats = new ShopStats { MeleeDamage = 4, RangedDamage = 20, Damage = 0.5f };
            string description = ShopTextFormatter.FormatProductDescription(weapon, stats);
            string combatStat = isMelee ? "Ближний бой" : "Дальний бой";
            Assert.That(description, Does.Contain("Урон: " + expectedDamage + " | (10 + 100% " + combatStat + ") × (1 + Урон / 100)"));
        }

        [Test]
        public void WeaponRangeShowsBaseFormulaAndCurrentPlayerBonus()
        {
            var weapon = new ShopProduct { IsWeapon = true, Range = 6 };
            var stats = new ShopStats { Range = 0.5f };
            string description = ShopTextFormatter.FormatProductDescription(weapon, stats);
            Assert.That(description, Does.Contain("Дальность: 9 м | 6 м × (1 + Дальность / 100)"));
        }

        [Test]
        public void ShotgunDamageFormulaAppliesToEachPellet()
        {
            var weapon = new ShopProduct { IsWeapon = true, IsShotgun = true, Damage = 5, Targets = 6 };
            var stats = new ShopStats { RangedDamage = 3, Damage = 0.5f };
            string description = ShopTextFormatter.FormatProductDescription(weapon, stats);
            Assert.That(description, Does.Contain("Урон: 12 × 6 дробин | (5 + 100% Дальний бой) × (1 + Урон / 100)"));
        }
    }
}
