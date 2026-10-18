using System;
using HordeMuncher.UI.Contracts;

namespace HordeMuncher.Tests.Shop
{
    public sealed class FakeShopView : IShopView
    {
        public event Action<int> OnBuyOfferClicked;
        public event Action<int> OnLockOfferClicked;
        public event Action<int> OnSellWeaponClicked;
        public event Action<int> OnMergeWeaponClicked;
        public event Action<int> OnUpgradeChoiceClicked;
        public event Action OnTakeItemRewardClicked;
        public event Action OnRecycleItemRewardClicked;
        public event Action OnRerollShopClicked;
        public event Action OnRerollUpgradesClicked;
        public event Action OnReadyButtonClicked;
        public event Action OnExitToMenuClicked;
        public event Action OnPreviousItemsPageClicked;
        public event Action OnNextItemsPageClicked;
        public event Action OnToggleStatsClicked;
        public event Action<string> OnSelectionChanged;

        public void Select(string selectionKey)
        {
            OnSelectionChanged?.Invoke(selectionKey);
        }

        public int ClickSubscriberCount =>
            (OnBuyOfferClicked?.GetInvocationList().Length ?? 0) + (OnLockOfferClicked?.GetInvocationList().Length ?? 0)
            + (OnSellWeaponClicked?.GetInvocationList().Length ?? 0)
            + (OnMergeWeaponClicked?.GetInvocationList().Length ?? 0)
            + (OnUpgradeChoiceClicked?.GetInvocationList().Length ?? 0)
            + (OnTakeItemRewardClicked?.GetInvocationList().Length ?? 0)
            + (OnRecycleItemRewardClicked?.GetInvocationList().Length ?? 0)
            + (OnRerollShopClicked?.GetInvocationList().Length ?? 0)
            + (OnRerollUpgradesClicked?.GetInvocationList().Length ?? 0)
            + (OnReadyButtonClicked?.GetInvocationList().Length ?? 0)
            + (OnExitToMenuClicked?.GetInvocationList().Length ?? 0)
            + (OnPreviousItemsPageClicked?.GetInvocationList().Length ?? 0)
            + (OnNextItemsPageClicked?.GetInvocationList().Length ?? 0)
            + (OnToggleStatsClicked?.GetInvocationList().Length ?? 0);
        public ShopViewData ViewData { get; private set; }
        public int RenderCount { get; private set; }
        public float ReadyHoldProgress { get; private set; }

        public void SetReadyHoldProgress(float progress)
        {
            ReadyHoldProgress = progress;
        }

        public void Render(ShopViewData viewData)
        {
            ViewData = viewData;
            RenderCount++;
        }

        public void MoveSelection(float horizontal, float vertical)
        {
        }

        public void SubmitSelection()
        {
        }

        public void LockSelectedOffer()
        {
        }

        public void MergeSelectedWeapon()
        {
        }

        public void ClickBuyOffer(int offerSlot = 0)
        {
            OnBuyOfferClicked?.Invoke(offerSlot);
        }

        public void ClickLockOffer(int offerSlot = 0)
        {
            OnLockOfferClicked?.Invoke(offerSlot);
        }

        public void ClickSellWeapon(int weaponSlot = 0)
        {
            OnSellWeaponClicked?.Invoke(weaponSlot);
        }

        public void ClickMergeWeapon(int weaponSlot = 0)
        {
            OnMergeWeaponClicked?.Invoke(weaponSlot);
        }

        public void ClickUpgradeChoice(int upgradeChoiceSlot = 0)
        {
            OnUpgradeChoiceClicked?.Invoke(upgradeChoiceSlot);
        }

        public void ClickRerollShop()
        {
            OnRerollShopClicked?.Invoke();
        }

        public void ClickRerollUpgrades()
        {
            OnRerollUpgradesClicked?.Invoke();
        }

        public void ClickReadyButton()
        {
            OnReadyButtonClicked?.Invoke();
        }

        public void ClickExitToMenu()
        {
            OnExitToMenuClicked?.Invoke();
        }

        public void ClickPreviousItemsPage()
        {
            OnPreviousItemsPageClicked?.Invoke();
        }

        public void ClickToggleStats()
        {
            OnToggleStatsClicked?.Invoke();
        }

        public void ClickNextItemsPage()
        {
            OnNextItemsPageClicked?.Invoke();
        }
    }
}
