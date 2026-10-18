using Unity.Mathematics;

namespace HordeMuncher
{
    public static class StatUpgradeRules
    {
        public static int GetBasePoints(BonusKind stat)
        {
            switch (stat)
            {
                case BonusKind.Health:
                    return 3;
                case BonusKind.MoveSpeed:
                case BonusKind.Pickup:
                    return 2;
                case BonusKind.Armor:
                case BonusKind.Regeneration:
                case BonusKind.MeleeDamage:
                case BonusKind.RangedDamage:
                    return 1;
                case BonusKind.CriticalChance:
                    return 2;
                case BonusKind.CriticalDamageMultiplier:
                    return 5;
                default:
                    return 3;
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
                    return points / 100f;
                case BonusKind.Regeneration:
                    return points / 10f;
                case BonusKind.Pickup:
                    return points * 0.05f;
                default:
                    return points;
            }
        }

        public static StatUpgradeRarity RollRarity(int luck, ref Random random)
        {
            float inverseLuckFactor = 1f / (1f + math.max(0, luck) / 100f);
            float commonWeight = 70 * inverseLuckFactor * inverseLuckFactor * inverseLuckFactor;
            float rareWeight = 25 * inverseLuckFactor * inverseLuckFactor;
            float epicWeight = 4 * inverseLuckFactor;
            float roll = random.NextFloat() * (commonWeight + rareWeight + epicWeight + 1);
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
