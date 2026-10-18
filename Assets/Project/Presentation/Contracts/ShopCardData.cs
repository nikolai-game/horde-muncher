using System;

namespace HordeMuncher.UI.Contracts
{
    public sealed class ShopCardData
    {
        public int CatalogId;
        public int WeaponSlot = -1;
        public int MergePartnerSlot = -1;
        public ShopCardData MergeResult;
        public string MergeNotice;
        public ShopBonusKind? UpgradeStat;
        public ShopStatChangeData[] StatChanges = Array.Empty<ShopStatChangeData>();
        public bool IsWeapon;
        public bool IsSold;
        public bool IsLocked;
        public string StatusText;
        public string PriceText;
        public bool IsAffordable = true;
        public string CountText;
        public string Name;
        public string Caption;
        public string RarityStyle;
        public string Description;
        public string Tooltip;
        public ShopButtonData[] Buttons = Array.Empty<ShopButtonData>();
    }
}
