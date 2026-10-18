using System;

namespace HordeMuncher.UI.Contracts
{
    public interface IShopView
    {
        event Action<int> OnBuyOfferClicked;
        event Action<int> OnLockOfferClicked;
        event Action<int> OnSellWeaponClicked;
        event Action<int> OnMergeWeaponClicked;
        event Action<int> OnUpgradeChoiceClicked;
        event Action OnTakeItemRewardClicked;
        event Action OnRecycleItemRewardClicked;
        event Action OnRerollShopClicked;
        event Action OnRerollUpgradesClicked;
        event Action OnReadyButtonClicked;
        event Action OnExitToMenuClicked;
        event Action OnPreviousItemsPageClicked;
        event Action OnNextItemsPageClicked;
        event Action OnToggleStatsClicked;
        event Action<string> OnSelectionChanged;
        void Render(ShopViewData viewData);
        void MoveSelection(float horizontal, float vertical);
        void SubmitSelection();
        void LockSelectedOffer();
        void MergeSelectedWeapon();
        void SetReadyHoldProgress(float progress);
    }
}
