using System;
using System.Text;
using HordeMuncher.UI.Contracts;

namespace HordeMuncher.UI.Shop.Presentation
{
    public static class ShopTextFormatter
    {
        private const float MIN_STAT_MULTIPLIER = 0.1f;

        public static string FormatBonusPercent(float bonusFraction) => (bonusFraction >= 0 ? "+" : "")
            + (bonusFraction * StatUnits.PERCENT_SCALE).ToString("0.#") + "%";

        public static string FormatProductDescription(ShopProduct product, ShopStats stats)
        {
            if (!product.IsWeapon)
            {
                var description = new StringBuilder();
                if (product.Amount != 0)
                {
                    description.Append(FormatItemStatChange(product.Bonus, product.Amount));
                }

                if (product.PenaltyAmount != 0)
                {
                    if (description.Length > 0)
                    {
                        description.Append('\n');
                    }

                    description.Append(FormatItemStatChange(product.Penalty, product.PenaltyAmount));
                }

                return description.ToString();
            }

            float combatBonus = product.IsMelee ? stats.MeleeDamage : stats.RangedDamage;
            int damage = Math.Max(1, (int)Math.Round(Math.Max(0, product.Damage + combatBonus) * Math.Max(0, 1 + stats.Damage)));
            var text = new StringBuilder();
            text.Append(product.IsMelee ? "Ближний бой\nУрон: " : "Дальний бой\nУрон: ").Append(damage);
            if (product.IsShotgun)
            {
                text.Append(" × ").Append(Math.Max(1, Math.Min(product.Targets, ArenaLimits.MAX_SHOTGUN_PELLETS))).Append(" дробин");
            }

            text.Append(" | (").Append(product.Damage).Append(" + 100% ");
            text.Append(product.IsMelee ? "Ближний бой" : "Дальний бой").Append(") × (1 + Урон / 100)");
            if (product.IsMelee)
            {
                text.Append("\nЦелей за удар: ").Append(product.Targets);
                text.Append("\nСектор удара: ").Append(product.Cone.ToString("0.#")).Append("°");
            }

            text.Append("\nИнтервал: ").Append((product.Interval / Math.Max(MIN_STAT_MULTIPLIER, 1
                + stats.AttackSpeed)).ToString("0.##")).Append(" с");
            text.Append("\nДальность: ").Append((product.Range * Math.Max(MIN_STAT_MULTIPLIER, 1 + stats.Range)).ToString("0.#")).Append(" м");
            text.Append(" | ").Append(product.Range.ToString("0.#")).Append(" м × (1 + Дальность / 100)");
            if (product.ExtraPierceTargets > 0)
            {
                text.Append("\nПробивает ещё целей: ").Append(product.ExtraPierceTargets);
            }

            if (product.KnockbackDistance > 0)
            {
                text.Append("\nОтбрасывает: ").Append(product.KnockbackDistance.ToString("0.##")).Append(" м за попадание");
            }

            if (product.RetargetOnKill)
            {
                text.Append("\nПосле гибели цели сразу переключается");
            }

            if (product.IsShotgun)
            {
                text.Append("\nРазброс: ").Append(product.Cone.ToString("0.#")).Append("°");
            }

            return text.ToString();
        }

        public static string FormatItemStatChange(ShopBonusKind stat, float amount)
        {
            float points = amount;
            switch (stat)
            {
                case ShopBonusKind.Damage:
                case ShopBonusKind.AttackSpeed:
                case ShopBonusKind.MoveSpeed:
                case ShopBonusKind.Range:
                case ShopBonusKind.CriticalChance:
                case ShopBonusKind.CriticalDamageMultiplier:
                    points *= StatUnits.PERCENT_SCALE;
                    break;
                case ShopBonusKind.Pickup:
                    points /= StatUnits.PICKUP_METERS_PER_POINT;
                    break;
                case ShopBonusKind.Regeneration:
                    points *= StatUnits.REGENERATION_POINTS_PER_HP;
                    break;
            }

            return (points > 0 ? "+" : "−") + ShopStatFormatter.FormatPoints(Math.Abs(points)) + " " + ShopStatFormatter.GetName(stat);
        }

        public static string FormatFeedback(ShopFeedback feedback)
        {
            switch (feedback)
            {
                case ShopFeedback.NotEnoughMoney:
                    return "Недостаточно ресурсов";
                case ShopFeedback.FullWeapons:
                    return "Все оружейные слоты заняты";
                case ShopFeedback.LastWeapon:
                    return "Последнее оружие нужно оставить для боя";
                case ShopFeedback.ResolveUpgrades:
                    return "Сначала выберите улучшения";
                case ShopFeedback.AlreadyReady:
                    return "Отмените готовность для покупок";
                case ShopFeedback.Invalid:
                    return "Предложение уже изменилось";
                default:
                    return "";
            }
        }
    }
}
