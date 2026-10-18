namespace HordeMuncher.UI.Contracts
{
    public struct ShopProduct
    {
        public string Name;
        public string Rarity;
        public int UpgradeCatalogId;
        public string Description;
        public bool IsWeapon;
        public bool IsShotgun;
        public bool IsMelee;
        public ShopBonusKind Bonus;
        public float Amount;
        public ShopBonusKind Penalty;
        public float PenaltyAmount;
        public int Price;
        public int Damage;
        public int Targets;
        public float Interval;
        public float Range;
        public float Cone;
        public int ExtraPierceTargets;
        public float KnockbackDistance;
        public bool RetargetOnKill;
    }
}
