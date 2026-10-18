using System;
using HordeMuncher.UI.Contracts;

namespace HordeMuncher.UI.Shop.Presentation
{
    public static class ShopStatFormatter
    {
        private const int MAX_ITEM_DROP_PERCENT = 25;
        private const float ITEM_DROP_PERCENT_PER_LUCK = 0.1f;

        public static string GetName(ShopBonusKind stat)
        {
            switch (stat)
            {
                case ShopBonusKind.Health: return "Здоровье";
                case ShopBonusKind.Damage: return "Урон";
                case ShopBonusKind.AttackSpeed: return "Скорость атаки";
                case ShopBonusKind.MoveSpeed: return "Скорость движения";
                case ShopBonusKind.Range: return "Дальность";
                case ShopBonusKind.Pickup: return "Радиус подбора";
                case ShopBonusKind.Armor: return "Броня";
                case ShopBonusKind.Regeneration: return "Регенерация";
                case ShopBonusKind.CriticalChance: return "Шанс крита";
                case ShopBonusKind.CriticalDamageMultiplier: return "Критический урон";
                case ShopBonusKind.MeleeDamage: return "Ближний бой";
                case ShopBonusKind.RangedDamage: return "Дальний бой";
                case ShopBonusKind.Luck: return "Удача";
                default: return "";
            }
        }

        public static float GetPoints(ShopBonusKind stat, ShopStats stats)
        {
            switch (stat)
            {
                case ShopBonusKind.Health: return stats.MaximumHealth;
                case ShopBonusKind.Damage: return stats.Damage * StatUnits.PERCENT_SCALE;
                case ShopBonusKind.AttackSpeed: return stats.AttackSpeed * StatUnits.PERCENT_SCALE;
                case ShopBonusKind.MoveSpeed: return stats.MoveSpeed * StatUnits.PERCENT_SCALE;
                case ShopBonusKind.Range: return stats.Range * StatUnits.PERCENT_SCALE;
                case ShopBonusKind.Pickup: return stats.Pickup / StatUnits.PICKUP_METERS_PER_POINT;
                case ShopBonusKind.Armor: return stats.Armor;
                case ShopBonusKind.Regeneration: return stats.Regeneration * StatUnits.REGENERATION_POINTS_PER_HP;
                case ShopBonusKind.CriticalChance: return stats.CriticalChance * StatUnits.PERCENT_SCALE;
                case ShopBonusKind.CriticalDamageMultiplier: return Math.Max(1, stats.CriticalDamageMultiplier) * StatUnits.PERCENT_SCALE;
                case ShopBonusKind.MeleeDamage: return stats.MeleeDamage;
                case ShopBonusKind.RangedDamage: return stats.RangedDamage;
                case ShopBonusKind.Luck: return stats.Luck;
                default: return 0;
            }
        }

        public static string FormatPoints(float points) => points.ToString("0.#");

        public static string GetExplanation(ShopBonusKind stat, float points)
        {
            string effect;
            switch (stat)
            {
                case ShopBonusKind.Health:
                    effect = "Каждое очко даёт 1 к максимальному здоровью. Улучшение также восстанавливает столько же здоровья живому герою.";
                    break;
                case ShopBonusKind.Damage:
                    effect = "Каждое очко добавляет 1% к бонусу урона всех видов оружия.";
                    break;
                case ShopBonusKind.AttackSpeed:
                    effect = "Каждое очко добавляет 1% к бонусу скорости атаки. Интервал атаки делится на (1 + очки / 100).";
                    break;
                case ShopBonusKind.MoveSpeed:
                    effect = "Каждое очко добавляет 1% к бонусу скорости движения героя.";
                    break;
                case ShopBonusKind.Range:
                    effect = "Каждое очко добавляет 1% к бонусу дальности оружия.";
                    break;
                case ShopBonusKind.Pickup:
                    effect = "Каждое очко увеличивает радиус подбора на 0,05 м. Текущий радиус: "
                        + (StatUnits.BASE_PICKUP_RADIUS + points * StatUnits.PICKUP_METERS_PER_POINT).ToString("0.##") + " м.";
                    break;
                case ShopBonusKind.Armor:
                    effect = "Каждое очко уменьшает урон от контакта с врагом на 1. Полученный удар наносит минимум 1 урон.";
                    break;
                case ShopBonusKind.Regeneration:
                    effect = "Каждые 10 очков восстанавливают 1 HP в секунду. Сейчас: "
                        + (points / StatUnits.REGENERATION_POINTS_PER_HP).ToString("0.##") + " HP/с.";
                    break;
                case ShopBonusKind.CriticalChance:
                    effect = "Каждое очко даёт 1% шанса критического удара. Максимум: 100 очков.";
                    break;
                case ShopBonusKind.CriticalDamageMultiplier:
                    effect = "100 очков соответствуют множителю ×1. Каждое очко добавляет 0,01 к множителю. Сейчас: ×"
                        + (points / StatUnits.PERCENT_SCALE).ToString("0.##") + ".";
                    break;
                case ShopBonusKind.MeleeDamage:
                    effect = "Каждое очко добавляет 1 к базовому урону оружия ближнего боя до применения общего бонуса урона.";
                    break;
                case ShopBonusKind.RangedDamage:
                    effect = "Каждое очко добавляет 1 к базовому урону оружия дальнего боя до применения общего бонуса урона.";
                    break;
                case ShopBonusKind.Luck:
                    float inverseLuckFactor = 1 / (1 + Math.Max(0, points) / StatUnits.PERCENT_SCALE);
                    float commonWeight = UpgradeRarityWeights.COMMON * inverseLuckFactor * inverseLuckFactor * inverseLuckFactor;
                    float rareWeight = UpgradeRarityWeights.RARE * inverseLuckFactor * inverseLuckFactor;
                    float epicWeight = UpgradeRarityWeights.EPIC * inverseLuckFactor;
                    float totalWeight = commonWeight + rareWeight + epicWeight + UpgradeRarityWeights.LEGENDARY;
                    effect = "Повышает шанс редких улучшений. Шансы каждой карточки сейчас: обычное "
                        + (StatUnits.PERCENT_SCALE * commonWeight / totalWeight).ToString("0.#") + "%, редкое "
                        + (StatUnits.PERCENT_SCALE * rareWeight / totalWeight).ToString("0.#") + "%, эпическое "
                        + (StatUnits.PERCENT_SCALE * epicWeight / totalWeight).ToString("0.#") + "%, легендарное "
                        + (StatUnits.PERCENT_SCALE * UpgradeRarityWeights.LEGENDARY / totalWeight).ToString("0.#")
                            + "%. Новая удача влияет на следующий набор."
                        + " Шанс предмета с побеждённого врага: " + Math.Min(MAX_ITEM_DROP_PERCENT, Math.Max(0,
                            points) * ITEM_DROP_PERCENT_PER_LUCK).ToString("0.#")
                        + "% (0,1% за очко, максимум 25%). Бонус предмета применяется при подборе.";
                    break;
                default:
                    effect = "";
                    break;
            }

            return GetName(stat) + " · " + FormatPoints(points) + "\n" + effect;
        }
    }
}
