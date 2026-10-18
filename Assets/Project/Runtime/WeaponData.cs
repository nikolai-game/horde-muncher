using System;
using UnityEngine;

namespace HordeMuncher
{
    [Serializable]
    public sealed class WeaponData
    {
        public string Name;
        [Tooltip("Иконка магазина и инвентаря. Если не задана, показываются первые буквы названия.")]
        public Sprite Icon;
        [TextArea]
        public string Description;
        public WeaponKind Weapon;
        public GameObject ModelPrefab;
        public Vector3 MuzzleOffset = new Vector3(0, 0, 0.55f);
        [Min(0.1f)]
        public float ProjectileSpeed = 18;
        [Min(0.01f)]
        public float ProjectileRadius = 0.12f;
        [Min(0.1f)]
        public float ProjectileLifetime = 2;
        [Range(0, 8)]
        public int ExtraPierceTargets;
        [Min(0)]
        public float KnockbackDistance;
        public bool RetargetOnKill;
        [Min(0)]
        public int Price = 8;
        [Min(0.01f)]
        public float Weight = 1;
        [Min(1)]
        public int Damage = 15;
        [Min(0.05f)]
        public float Interval = 0.25f;
        [Min(0.1f)]
        public float Range = 9;
        [Min(1)]
        public int Targets = 1;
        [Range(1, 180)]
        public float Cone = 50;
        [Header("Редкости (Common — базовые параметры выше)")]
        public WeaponTierData Rare = new WeaponTierData();
        public WeaponTierData Epic = new WeaponTierData();
        public WeaponTierData Legendary = new WeaponTierData();
    }
}
