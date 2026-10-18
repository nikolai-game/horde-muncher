using Unity.Collections;
using Unity.Entities;

namespace HordeMuncher
{
    public struct CatalogEntry : IBufferElementData
    {
        public FixedString64Bytes Name;
        public FixedString128Bytes Description;
        public GoodsKind Kind;
        public WeaponKind Weapon;
        public WeaponRarity Rarity;
        public int UpgradeCatalogId;
        public BonusKind Bonus;
        public float Amount;
        public BonusKind Penalty;
        public float PenaltyAmount;
        public float Weight;
        public float Interval;
        public float Range;
        public float Cone;
        public int Price;
        public int Damage;
        public int Targets;
        public float ProjectileSpeed;
        public float ProjectileRadius;
        public float ProjectileLifetime;
        public float KnockbackDistance;
        public int ExtraPierceTargets;
        public bool RetargetOnKill;
        public Unity.Mathematics.float3 MuzzleOffset;
    }
}
