using Unity.Entities;

namespace HordeMuncher
{
    public struct EconomyConfig : IComponentData
    {
        public float ResourceAttractionDuration;
        public float WeaponOrbitRadius;
        public float WeaponHeight;
        public float WeaponSellFraction;
        public int WeaponSlots;
        public int StartingWeapon;
        public int StartingResources;
        public int FirstLevelXp;
        public int LevelXpGrowth;
        public int RerollPrice;
        public int RerollGrowth;
        public int UpgradeRerollPrice;
        public int UpgradeRerollGrowth;
        public float EnemyPriceGrowth;
    }
}
