using System;

namespace HordeMuncher.UI.Contracts
{
    public static class ShopActionAvailability
    {
        public static bool CanExecute(ShopState state, ShopCommandKind command, int commandSlot)
        {
            if (state == null || !state.IsOpen || !state.CanControl)
            {
                return false;
            }

            bool hasItemRewards = state.PendingItemRewards.Length > 0;
            bool isRewardCommand = command == ShopCommandKind.Upgrade || command == ShopCommandKind.RerollUpgrades
                || command == ShopCommandKind.TakeItemReward || command == ShopCommandKind.RecycleItemReward;
            if ((state.IsRewardScreen || hasItemRewards) && !isRewardCommand)
            {
                return false;
            }

            if (command == ShopCommandKind.Ready)
            {
                return state.PendingUpgrades == 0;
            }

            if (state.Ready || (state.PendingUpgrades > 0 && command != ShopCommandKind.Upgrade
                && command != ShopCommandKind.RerollUpgrades))
            {
                return false;
            }

            switch (command)
            {
                case ShopCommandKind.TakeItemReward:
                case ShopCommandKind.RecycleItemReward:
                    return state.PendingUpgrades == 0 && hasItemRewards && IsCatalogIdValid(state, state.PendingItemRewards[0]);
                case ShopCommandKind.Buy:
                    if (commandSlot < 0 || commandSlot >= state.Offers.Length)
                    {
                        return false;
                    }

                    ShopOfferState offer = state.Offers[commandSlot];
                    return !offer.Sold && IsCatalogIdValid(state, offer.CatalogId) && state.Resources >= offer.Price
                        && (!state.Catalog[offer.CatalogId].IsWeapon || state.Weapons.Length < state.WeaponSlots
                            || CanMergeOffer(state, offer.CatalogId));
                case ShopCommandKind.Lock:
                    return commandSlot >= 0 && commandSlot < state.Offers.Length && !state.Offers[commandSlot].Sold
                        && IsCatalogIdValid(state, state.Offers[commandSlot].CatalogId);
                case ShopCommandKind.Reroll:
                    return state.Resources >= state.RerollPrice && Array.Exists(state.Offers, offer => !offer.Locked || offer.Sold);
                case ShopCommandKind.Sell:
                    return state.Weapons.Length > 1 && commandSlot >= 0 && commandSlot < state.Weapons.Length
                        && IsCatalogIdValid(state, state.Weapons[commandSlot])
                        && state.Catalog[state.Weapons[commandSlot]].IsWeapon;
                case ShopCommandKind.Merge:
                    if (commandSlot < 0 || commandSlot >= state.Weapons.Length)
                    {
                        return false;
                    }

                    int catalogId = state.Weapons[commandSlot];
                    return CanMergeOffer(state, catalogId)
                        && Array.FindIndex(state.Weapons, weaponCatalogId => weaponCatalogId == catalogId) !=
                            Array.FindLastIndex(state.Weapons, weaponCatalogId => weaponCatalogId == catalogId);
                case ShopCommandKind.RerollUpgrades:
                    return state.PendingUpgrades > 0 && state.Resources >= state.UpgradeRerollPrice;
                case ShopCommandKind.Upgrade:
                    return state.PendingUpgrades > 0 && commandSlot >= 0 && commandSlot < state.UpgradeChoices.Length
                        && state.UpgradeChoices[commandSlot].Points > 0;
                default:
                    return false;
            }
        }

        public static bool CanMergeOffer(ShopState state, int catalogId)
        {
            if (!IsCatalogIdValid(state, catalogId) || !state.Catalog[catalogId].IsWeapon)
            {
                return false;
            }

            int upgradeCatalogId = state.Catalog[catalogId].UpgradeCatalogId;
            return upgradeCatalogId > 0 && IsCatalogIdValid(state, upgradeCatalogId)
                && Array.IndexOf(state.Weapons, catalogId) >= 0;
        }

        public static bool IsCatalogIdValid(ShopState state, int catalogId) => catalogId >= 0 && catalogId < state.Catalog.Length;
    }
}
