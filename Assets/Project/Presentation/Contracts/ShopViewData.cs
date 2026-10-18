using System;

namespace HordeMuncher.UI.Contracts
{
    public sealed class ShopViewData
    {
        public bool IsVisible;
        public bool CanControl;
        public ShopNavigationState Navigation;
        public int PlayerNumber;
        public bool IsLeveling;
        public bool IsRewardScreen;
        public bool IsShowingStats;
        public bool UsesPages;
        public string Title;
        public string MoneyText;
        public string PlayerText;
        public string NextWaveText;
        public string FeedbackText;
        public int ItemPage;
        public int ItemPages;
        public int EmptyWeaponSlots;
        public int WeaponCount;
        public int WeaponSlots;
        public ShopButtonData Reroll;
        public ShopButtonData Ready;
        public ShopCardData[] Offers = Array.Empty<ShopCardData>();
        public ShopCardData[] Items = Array.Empty<ShopCardData>();
        public ShopCardData[] Weapons = Array.Empty<ShopCardData>();
        public ShopStatData[] Stats = Array.Empty<ShopStatData>();
    }
}
