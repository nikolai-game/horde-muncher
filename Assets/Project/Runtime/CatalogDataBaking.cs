using System;
using System.Collections.Generic;
using UnityEngine;

namespace HordeMuncher
{
    public static class CatalogDataBaking
    {
        public static IEnumerable<CatalogEntry> Build(EconomySettings settings)
        {
            for (int weaponIndex = 0; weaponIndex < (settings.Weapons?.Length ?? 0); weaponIndex++)
            {
                WeaponData weapon = settings.Weapons[weaponIndex];
                if (weapon == null)
                {
                    throw new InvalidOperationException("The weapon catalog contains an empty entry.");
                }

                yield return BuildWeapon(weapon, null, WeaponRarity.Common,
                    settings.Weapons.Length + (settings.Items?.Length ?? 0) + weaponIndex * 3);
            }

            foreach (ItemData item in settings.Items ?? Array.Empty<ItemData>())
            {
                if (item == null)
                {
                    throw new InvalidOperationException("The item catalog contains an empty entry.");
                }

                yield return new CatalogEntry
                {
                    Name = item.Name ?? "",
                    Description = item.Description ?? "",
                    Kind = GoodsKind.Item,
                    Bonus = item.Bonus,
                    Amount = item.Amount,
                    Penalty = item.Penalty,
                    PenaltyAmount = Mathf.Min(0, item.PenaltyAmount),
                    Price = Mathf.Max(0, item.Price),
                    Weight = Mathf.Max(0.01f, item.Weight)
                };
            }

            int baseCatalogLength = (settings.Weapons?.Length ?? 0) + (settings.Items?.Length ?? 0);
            for (int weaponIndex = 0; weaponIndex < (settings.Weapons?.Length ?? 0); weaponIndex++)
            {
                WeaponData weapon = settings.Weapons[weaponIndex];
                int rareCatalogId = baseCatalogLength + weaponIndex * 3;
                yield return BuildWeapon(weapon, weapon.Rare, WeaponRarity.Rare, rareCatalogId + 1);
                yield return BuildWeapon(weapon, weapon.Epic, WeaponRarity.Epic, rareCatalogId + 2);
                yield return BuildWeapon(weapon, weapon.Legendary, WeaponRarity.Legendary, 0);
            }
        }

        private static CatalogEntry BuildWeapon(WeaponData weapon, WeaponTierData tier, WeaponRarity rarity, int upgradeCatalogId)
        {
            return new CatalogEntry
            {
                Name = weapon.Name ?? "",
                Description = weapon.Description ?? "",
                Kind = GoodsKind.Weapon,
                Weapon = weapon.Weapon,
                Rarity = rarity,
                UpgradeCatalogId = upgradeCatalogId,
                Price = Mathf.Max(0, tier?.Price ?? weapon.Price),
                Weight = Mathf.Max(0.01f, tier?.Weight ?? weapon.Weight),
                Damage = Mathf.Max(1, tier?.Damage ?? weapon.Damage),
                Interval = Mathf.Max(0.05f, tier?.Interval ?? weapon.Interval),
                Range = Mathf.Max(0.1f, tier?.Range ?? weapon.Range),
                Targets = Mathf.Max(1, tier?.Targets ?? weapon.Targets),
                Cone = Mathf.Clamp(tier?.Cone ?? weapon.Cone, 1, 180),
                ProjectileSpeed = Mathf.Max(0.1f, tier?.ProjectileSpeed ?? weapon.ProjectileSpeed),
                ProjectileRadius = Mathf.Max(0.01f, tier?.ProjectileRadius ?? weapon.ProjectileRadius),
                ProjectileLifetime = Mathf.Max(0.1f, tier?.ProjectileLifetime ?? weapon.ProjectileLifetime),
                MuzzleOffset = weapon.MuzzleOffset,
                ExtraPierceTargets = Mathf.Clamp(tier?.ExtraPierceTargets ?? weapon.ExtraPierceTargets, 0, 8),
                KnockbackDistance = Mathf.Max(0, tier?.KnockbackDistance ?? weapon.KnockbackDistance),
                RetargetOnKill = tier?.RetargetOnKill ?? weapon.RetargetOnKill
            };
        }
    }
}
