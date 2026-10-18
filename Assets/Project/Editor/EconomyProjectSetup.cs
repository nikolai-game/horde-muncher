using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HordeMuncher.Editor
{
    public static class EconomyProjectSetup
    {
        [MenuItem("Horde Muncher/Install Economy")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode first.");
            }

            const string PATH = "Assets/Project/Configs/Economy.asset";
            EconomySettings economy = AssetDatabase.LoadAssetAtPath<EconomySettings>(PATH);
            if (economy == null)
            {
                economy = ScriptableObject.CreateInstance<EconomySettings>();
                economy.Weapons = DefaultWeapons();
                economy.Items = DefaultItems();
                AssetDatabase.CreateAsset(economy, PATH);
            }

            const string SCENE_PATH = "Assets/Project/Scenes/ArenaEntities.unity";
            Scene shared = SceneManager.GetSceneByPath(SCENE_PATH);
            bool wasOpen = shared.IsValid() && shared.isLoaded;
            if (!wasOpen)
            {
                shared = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Additive);
            }

            try
            {
                foreach (GameObject root in shared.GetRootGameObjects())
                {
                    foreach (ArenaAuthoring authoring in root.GetComponentsInChildren<ArenaAuthoring>(true))
                    {
                        authoring.Economy = economy;
                        EditorUtility.SetDirty(authoring);
                    }
                }

                EditorSceneManager.MarkSceneDirty(shared);
                EditorSceneManager.SaveScene(shared);
            }
            finally
            {
                if (!wasOpen)
                {
                    EditorSceneManager.CloseScene(shared, true);
                }
            }

            ArenaMenu menu = UnityEngine.Object.FindFirstObjectByType<ArenaMenu>();
            if (menu == null)
            {
                throw new InvalidOperationException("Open the Arena scene first.");
            }

            if (menu.GetComponent<ShopUiCompositionRoot>() == null)
            {
                menu.gameObject.AddComponent<ShopUiCompositionRoot>();
            }

            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            EditorSceneManager.SaveScene(menu.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        public static WeaponData[] DefaultWeapons()
        {
            WeaponData[] weapons =
            {
                new WeaponData
                {
                    Name = "Пистолет",
                    Description = "30 урона · 0,25 с · 9 м · пробивает одну цель",
                    Weapon = WeaponKind.Pistol,
                    ExtraPierceTargets = 1,
                    ProjectileSpeed = 40,
                    Price = 18,
                    Weight = 3,
                    Damage = 30,
                    Interval = 0.25f,
                    Range = 9
                },
                new WeaponData
                {
                    Name = "Дробовик",
                    Description = "12 урона за дробину · 5 дробин · 0,7 с · 6 м",
                    Weapon = WeaponKind.Shotgun,
                    KnockbackDistance = 0.25f,
                    ProjectileSpeed = 36,
                    Price = 24,
                    Weight = 1,
                    Damage = 12,
                    Interval = 0.7f,
                    Range = 6,
                    Targets = 5,
                    Cone = 50
                },
                new WeaponData
                {
                    Name = "Автомат",
                    Description = "10 урона · 0,1 с · 8 м · одна цель",
                    Weapon = WeaponKind.Automatic,
                    RetargetOnKill = true,
                    ProjectileSpeed = 48,
                    Price = 24,
                    Weight = 1,
                    Damage = 10,
                    Interval = 0.1f,
                    Range = 8
                },
                CreateMeleeWeapon("Нож", WeaponKind.Knife, 30, 0.25f, 2.2f, 2, 55, 0.1f, 18),
                CreateMeleeWeapon("Топор", WeaponKind.Axe, 45, 0.65f, 2.8f, 5, 120, 0.6f, 26)
            };

            foreach (WeaponData weapon in weapons)
            {
                if (WeaponCombatRules.IsMelee(weapon.Weapon))
                {
                    continue;
                }

                weapon.Rare = CreateRangedTier(weapon, 2, 0.9f, 1, 0.35f, 1.6f);
                weapon.Epic = CreateRangedTier(weapon, 4, 0.8f, 1.1f, 0.12f, 2.4f);
                weapon.Legendary = CreateRangedTier(weapon, 8, 0.7f, 1.2f, 0.04f, 3.4f);
            }

            return weapons;
        }

        public static ItemData[] DefaultItems() => new[]
        {
            CreateItemEntry("Сердце", "+6 Здоровье", BonusKind.Health, 6, 24),
            CreateItemEntry("Порох", "+10 Урон", BonusKind.Damage, 0.1f, 22),
            CreateItemEntry("Пружина", "+10 Скорость атаки", BonusKind.AttackSpeed, 0.1f, 22),
            CreateItemEntry("Ботинки", "+10 Скорость движения", BonusKind.MoveSpeed, 0.1f, 20),
            CreateItemEntry("Прицел", "+15 Дальность", BonusKind.Range, 0.15f, 18),
            CreateItemEntry("Магнит", "+15 Радиус подбора", BonusKind.Pickup, 0.75f, 18),
            CreateItemEntry("Бронепластина", "+1 Броня", BonusKind.Armor, 1, 18),
            CreateItemEntry("Росток", "+10 Регенерация", BonusKind.Regeneration, 1, 28),
            CreateTradeoffItem("Кастет", "+4 Ближний бой, −2 Дальний бой", BonusKind.MeleeDamage, 4, BonusKind.RangedDamage, -2, 22),
            CreateTradeoffItem("Патронташ", "+3 Дальний бой, −3 Ближний бой", BonusKind.RangedDamage, 3, BonusKind.MeleeDamage, -3, 20),
            CreateTradeoffItem("Берсерк", "+20 Урон, −2 Броня", BonusKind.Damage, 0.2f, BonusKind.Armor, -2, 30),
            CreateTradeoffItem("Тяжёлый жилет", "+3 Броня, −8 Скорость движения", BonusKind.Armor, 3, BonusKind.MoveSpeed, -0.08f, 28),
            CreateTradeoffItem("Сервопривод", "+20 Скорость атаки, −10 Урон", BonusKind.AttackSpeed, 0.2f, BonusKind.Damage, -0.1f, 26),
            CreateTradeoffItem("Снайперский монокль", "+4 Дальний бой, −12 Скорость атаки",
                BonusKind.RangedDamage, 4, BonusKind.AttackSpeed, -0.12f, 26),
            CreateTradeoffItem("Кровавый талисман", "+6 Ближний бой, −15 Здоровье", BonusKind.MeleeDamage, 6, BonusKind.Health, -15, 28),
            CreateTradeoffItem("Походная фляга", "+20 Регенерация, −15 Дальность", BonusKind.Regeneration, 2, BonusKind.Range, -0.15f, 32),
            CreateTradeoffItem("Облегчённая обувь", "+18 Скорость движения, −1 Броня", BonusKind.MoveSpeed, 0.18f, BonusKind.Armor, -1, 26),
            CreateTradeoffItem("Стеклянная линза", "+12 Шанс крита, −15 Здоровье", BonusKind.CriticalChance, 0.12f, BonusKind.Health, -15, 28)
        };

        public static CatalogEntry[] DefaultCatalog()
        {
            EconomySettings settings = ScriptableObject.CreateInstance<EconomySettings>();
            try
            {
                settings.Weapons = DefaultWeapons();
                settings.Items = DefaultItems();
                return new System.Collections.Generic.List<CatalogEntry>(CatalogDataBaking.Build(settings)).ToArray();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        private static WeaponTierData CreateRangedTier(WeaponData weapon,
            int powerMultiplier,
            float intervalMultiplier,
            float rangeMultiplier,
            float weightMultiplier,
            float priceMultiplier) => new WeaponTierData
        {
            Damage = weapon.Damage * powerMultiplier,
            Interval = weapon.Interval * intervalMultiplier,
            Range = weapon.Range * rangeMultiplier,
            Targets = weapon.Targets,
            Cone = weapon.Cone,
            ProjectileSpeed = weapon.ProjectileSpeed,
            ProjectileRadius = weapon.ProjectileRadius,
            ProjectileLifetime = weapon.ProjectileLifetime,
            ExtraPierceTargets = weapon.ExtraPierceTargets,
            KnockbackDistance = weapon.KnockbackDistance,
            RetargetOnKill = weapon.RetargetOnKill,
            Price = Mathf.RoundToInt(weapon.Price * priceMultiplier),
            Weight = weapon.Weight * weightMultiplier
        };

        private static WeaponData CreateMeleeWeapon(string name,
            WeaponKind kind,
            int damage,
            float interval,
            float range,
            int targets,
            float cone,
            float knockbackDistance,
            int price) => new WeaponData
        {
            Name = name,
            Description = "Удар в секторе перед игроком",
            Weapon = kind,
            Damage = damage,
            Interval = interval,
            Range = range,
            Targets = targets,
            Cone = cone,
            KnockbackDistance = knockbackDistance,
            Price = price,
            Weight = 2,
            Rare = CreateMeleeTier(damage, interval, range, targets, cone, knockbackDistance, price, 1.4f, 0.7f, 1.6f),
            Epic = CreateMeleeTier(damage, interval, range, targets, cone, knockbackDistance, price, 1.9f, 0.35f, 2.4f),
            Legendary = CreateMeleeTier(damage, interval, range, targets, cone, knockbackDistance, price, 2.6f, 0.15f, 3.4f)
        };

        private static WeaponTierData CreateMeleeTier(int damage,
            float interval,
            float range,
            int targets,
            float cone,
            float knockbackDistance,
            int price,
            float powerMultiplier,
            float weight,
            float priceMultiplier) => new WeaponTierData
        {
            Damage = Mathf.RoundToInt(damage * powerMultiplier),
            Interval = interval / Mathf.Sqrt(powerMultiplier),
            Range = range,
            Targets = targets,
            Cone = cone,
            KnockbackDistance = knockbackDistance,
            Price = Mathf.RoundToInt(price * priceMultiplier),
            Weight = weight
        };

        private static ItemData CreateTradeoffItem(string name,
            string description,
            BonusKind bonus,
            float amount,
            BonusKind penalty,
            float penaltyAmount,
            int price)
        {
            ItemData item = CreateItemEntry(name, description, bonus, amount, price);
            item.Penalty = penalty;
            item.PenaltyAmount = penaltyAmount;
            return item;
        }

        private static ItemData CreateItemEntry(string name, string description, BonusKind bonus, float amount, int price) => new ItemData
        {
            Name = name,
            Description = description,
            Bonus = bonus,
            Amount = amount,
            Price = price,
            Weight = 2
        };
    }
}
