using System;

namespace HordeMuncher.UI.Contracts
{
    public sealed class ShopState
    {
        public bool CanControl = true;
        public uint Revision;
        public bool IsOpen;
        public bool IsRewardScreen;
        public int[] PendingItemRewards = Array.Empty<int>();
        public ShopNavigationState Navigation;
        public int PlayerNumber;
        public int WaveNumber;
        public int Resources;
        public int Level;
        public int Experience;
        public int NextLevelExperience;
        public int PendingUpgrades;
        public bool Ready;
        public ShopFeedback Feedback;
        public ShopStats Stats;
        public int WeaponSlots;
        public int RerollPrice;
        public int UpgradeRerollPrice;
        public float SellFraction;
        public ShopProduct[] Catalog = Array.Empty<ShopProduct>();
        public ShopOfferState[] Offers = Array.Empty<ShopOfferState>();
        public ShopOwnedItem[] Items = Array.Empty<ShopOwnedItem>();
        public int[] Weapons = Array.Empty<int>();
        public ShopUpgradeChoice[] UpgradeChoices = Array.Empty<ShopUpgradeChoice>();
    }
}
