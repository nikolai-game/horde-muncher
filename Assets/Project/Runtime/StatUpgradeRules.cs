using Unity.Mathematics;

namespace HordeMuncher
{
    public static class StatUpgradeRules
    {
        private const int DEFAULT_UPGRADE_POINTS = 3;
        private const int SPEED_UPGRADE_POINTS = 2;
        private const int PICKUP_UPGRADE_POINTS = 2;
        private const int CRITICAL_CHANCE_POINTS = 2;
        private const int FLAT_STAT_UPGRADE_POINTS = 1;
        private const int CRITICAL_MULTIPLIER_POINTS = 5;

        public static int GetBasePoints(BonusKind stat)
        {
            switch (stat)
            {
                case BonusKind.Health:
                    return DEFAULT_UPGRADE_POINTS;
                case BonusKind.MoveSpeed:
                    return SPEED_UPGRADE_POINTS;
                case BonusKind.Pickup:
                    return PICKUP_UPGRADE_POINTS;
                case BonusKind.Armor:
                case BonusKind.Regeneration:
                case BonusKind.MeleeDamage:
                case BonusKind.RangedDamage:
                    return FLAT_STAT_UPGRADE_POINTS;
                case BonusKind.CriticalChance:
                    return CRITICAL_CHANCE_POINTS;
                case BonusKind.CriticalDamageMultiplier:
                    return CRITICAL_MULTIPLIER_POINTS;
                default:
                    return DEFAULT_UPGRADE_POINTS;
            }
        }

        public static float ToBonusAmount(BonusKind stat, int points)
        {
            switch (stat)
            {
                case BonusKind.Damage:
                case BonusKind.AttackSpeed:
                case BonusKind.MoveSpeed:
                case BonusKind.Range:
                case BonusKind.CriticalChance:
                case BonusKind.CriticalDamageMultiplier:
                    return points / StatUnits.PERCENT_SCALE;
                case BonusKind.Regeneration:
                    return points / StatUnits.REGENERATION_POINTS_PER_HP;
                case BonusKind.Pickup:
                    return points * StatUnits.PICKUP_METERS_PER_POINT;
                default:
                    return points;
            }
        }

        public static StatUpgradeRarity RollRarity(int luck, ref Random random)
        {
            float inverseLuckFactor = 1f / (1f + math.max(0, luck) / StatUnits.PERCENT_SCALE);
            float commonWeight = UpgradeRarityWeights.COMMON * inverseLuckFactor * inverseLuckFactor * inverseLuckFactor;
            float rareWeight = UpgradeRarityWeights.RARE * inverseLuckFactor * inverseLuckFactor;
            float epicWeight = UpgradeRarityWeights.EPIC * inverseLuckFactor;
            float roll = random.NextFloat() * (commonWeight + rareWeight + epicWeight + UpgradeRarityWeights.LEGENDARY);
            if (roll < commonWeight)
            {
                return StatUpgradeRarity.Common;
            }

            if (roll < commonWeight + rareWeight)
            {
                return StatUpgradeRarity.Rare;
            }

            return roll < commonWeight + rareWeight + epicWeight ? StatUpgradeRarity.Epic : StatUpgradeRarity.Legendary;
        }
    }
}
