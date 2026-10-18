using System;
using UnityEngine;

namespace HordeMuncher
{
    [CreateAssetMenu(menuName = "Horde Muncher/Economy Settings")]
    public sealed class EconomySettings : ScriptableObject
    {

        [Range(1, 6)]
        public int WeaponSlots = 6;
        [Range(0, 1)]
        public float WeaponSellFraction = 0.5f;
        [Min(0)]
        public int StartingWeapon;
        [Min(0)]
        public int StartingResources = 0;
        [Min(1)]
        public int FirstLevelXp = 5;
        [Min(0)]
        public int LevelXpGrowth = 5;
        [Min(0)]
        public int RerollPrice = 2;
        [Min(0)]
        public int RerollGrowth = 1;
        [Min(1)]
        public int UpgradeRerollPrice = 2;
        [Min(1)]
        public int UpgradeRerollGrowth = 1;
        [Min(0)]
        [Tooltip("Price markup for each doubling of the first-wave enemy count. "
            + "0.35 adds 35% of base price per doubling; extra income increases purchasing power.")]
        public float EnemyPriceGrowth = 0.35f;
        [Min(0)]
        public float WeaponOrbitRadius = 1.1f;
        public float WeaponHeight = 0.15f;
        [Min(0.01f)]
        public float ResourceAttractionDuration = 0.5f;
        public AnimationCurve ResourceAttractionCurve = new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 2, 2));
        public WeaponData[] Weapons = Array.Empty<WeaponData>();
        public ItemData[] Items = Array.Empty<ItemData>();

        public bool TryGetWeapon(int catalogId, out WeaponData weapon)
        {
            weapon = null;
            if (Weapons == null || catalogId < 0 || catalogId >= Weapons.Length + (Items?.Length ?? 0)
                + Weapons.Length * WeaponCatalogLayout.UPGRADED_TIER_COUNT)
            {
                return false;
            }

            int weaponIndex = catalogId;
            if (catalogId >= Weapons.Length)
            {
                int tierIndex = catalogId - Weapons.Length - (Items?.Length ?? 0);
                if (tierIndex < 0)
                {
                    return false;
                }

                weaponIndex = tierIndex / WeaponCatalogLayout.UPGRADED_TIER_COUNT;
            }

            weapon = Weapons[weaponIndex];
            return weapon != null;
        }

        public bool TryGetIcon(int catalogId, out Sprite icon)
        {
            icon = null;
            if (TryGetWeapon(catalogId, out WeaponData weapon))
            {
                icon = weapon.Icon;
                return icon != null;
            }

            int itemIndex = catalogId - (Weapons?.Length ?? 0);
            if (Items == null || itemIndex < 0 || itemIndex >= Items.Length || Items[itemIndex] == null)
            {
                return false;
            }

            icon = Items[itemIndex].Icon;
            return icon != null;
        }
    }
}
