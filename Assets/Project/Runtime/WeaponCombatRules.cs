using Unity.Mathematics;

namespace HordeMuncher
{
    public static class WeaponCombatRules
    {
        public static bool IsMelee(WeaponKind weapon) => weapon == WeaponKind.Knife || weapon == WeaponKind.Axe;

        public static float CalculateAttackRange(float baseRange, float rangeBonus) => baseRange * math.max(0.1f, 1 + rangeBonus);

        public static int CalculateDamage(CatalogEntry weapon, PlayerBonuses bonuses, bool isCritical)
        {
            float combatBonus = IsMelee(weapon.Weapon) ? bonuses.MeleeDamage : bonuses.RangedDamage;
            float criticalMultiplier = isCritical ? math.max(1, bonuses.CriticalDamageMultiplier) : 1;
            return math.max(1, (int)math.round(math.max(0, weapon.Damage + combatBonus)
                * math.max(0, 1 + bonuses.Damage) * criticalMultiplier));
        }
    }
}
