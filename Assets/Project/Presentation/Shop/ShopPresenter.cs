using System;
using HordeMuncher.UI.Contracts;

namespace HordeMuncher.UI.Shop.Presentation
{
    public sealed class ShopPresenter : IDisposable
    {
        private const double CONFIRMATION_DELAY = 2;
        private const double READY_HOLD_DURATION = 1;
        public event Action OnExitToMenuRequested;
        private readonly IShopStateSource _stateSource;
        private readonly IShopCommands _commands;
        private readonly IShopView _view;
        private ShopState _state;
        private ShopState _renderedState;
        private uint _pendingRevision;
        private double _currentTime;
        private double _commandSentAt;
        private double _readyHoldStartedAt = -1;
        private bool _readyHoldCompleted;
        private string _selectionKey = "";
        private int _itemPage;
        private bool _isRenderRequired;
        private bool _isShowingStats;
        private bool _usesPages;
        private bool _disposed;
        private string _feedbackNotice = "";
        public bool IsVisible { get; private set; }
        public bool IsPending { get; private set; }

        public ShopPresenter(IShopStateSource stateSource, IShopCommands commands, IShopView view)
        {
            _stateSource = stateSource ?? throw new ArgumentNullException(nameof(stateSource));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _view.OnBuyOfferClicked += BuyOfferClickedHandler;
            _view.OnLockOfferClicked += LockOfferClickedHandler;
            _view.OnSellWeaponClicked += SellWeaponClickedHandler;
            _view.OnMergeWeaponClicked += MergeWeaponClickedHandler;
            _view.OnUpgradeChoiceClicked += UpgradeChoiceClickedHandler;
            _view.OnTakeItemRewardClicked += TakeItemRewardClickedHandler;
            _view.OnRecycleItemRewardClicked += RecycleItemRewardClickedHandler;
            _view.OnRerollShopClicked += RerollShopClickedHandler;
            _view.OnRerollUpgradesClicked += RerollUpgradesClickedHandler;
            _view.OnReadyButtonClicked += ReadyButtonClickedHandler;
            _view.OnExitToMenuClicked += ExitToMenuClickedHandler;
            _view.OnPreviousItemsPageClicked += PreviousItemsPageClickedHandler;
            _view.OnNextItemsPageClicked += NextItemsPageClickedHandler;
            _view.OnToggleStatsClicked += ToggleStatsClickedHandler;
            _view.OnSelectionChanged += SelectionChangedHandler;
        }

        public void Refresh(double currentTime)
        {
            if (_disposed)
            {
                return;
            }

            _currentTime = currentTime;
            if (!_stateSource.TryRead(out ShopState state) || !state.IsOpen)
            {
                if (IsVisible)
                {
                    _view.Render(new ShopViewData());
                }

                IsVisible = false;
                IsPending = false;
                _state = null;
                _renderedState = null;
                _feedbackNotice = "";
                _selectionKey = "";
                _itemPage = 0;
                _isShowingStats = false;
                return;
            }

            if (_state != null && state.Revision != _state.Revision)
            {
                _feedbackNotice = "";
                _isRenderRequired = true;
            }

            if (IsPending && state.Revision != _pendingRevision)
            {
                IsPending = false;
                _feedbackNotice = "";
                _isRenderRequired = true;
            }

            if (IsPending && currentTime - _commandSentAt >= CONFIRMATION_DELAY && _feedbackNotice == "Ожидаем подтверждение сервера…")
            {
                _feedbackNotice = "Подтверждение задерживается. Ожидаем ответ сервера…";
                _isRenderRequired = true;
            }

            _state = state;
            if (!state.CanControl)
            {
                _itemPage = state.Navigation.ItemPage;
                _isShowingStats = state.Navigation.IsShowingStats;
            }

            IsVisible = true;
            if (_isRenderRequired || !ShopStateComparer.AreEqual(state, _renderedState))
            {
                Render();
            }

            PublishNavigation();
        }

        public void SetPlayerCount(int playerCount)
        {
            bool usesPages = playerCount > 1;
            if (_disposed || _usesPages == usesPages)
            {
                return;
            }

            _usesPages = usesPages;
            _itemPage = 0;
            _isShowingStats = false;
            _isRenderRequired = true;
        }

        public void UpdateReadyHold(bool isHeld, double currentTime)
        {
            if (!isHeld || _disposed || !IsVisible || IsPending || _isShowingStats
                || !ShopActionAvailability.CanExecute(_state, ShopCommandKind.Ready, 0))
            {
                _readyHoldStartedAt = -1;
                if (!isHeld)
                {
                    _readyHoldCompleted = false;
                }

                _view.SetReadyHoldProgress(0);
                return;
            }

            if (_readyHoldCompleted)
            {
                return;
            }

            if (_readyHoldStartedAt < 0)
            {
                _readyHoldStartedAt = currentTime;
            }

            float progress = (float)Math.Max(0, Math.Min(1, (currentTime - _readyHoldStartedAt) / READY_HOLD_DURATION));
            _view.SetReadyHoldProgress(progress);
            if (progress >= 1)
            {
                _readyHoldCompleted = true;
                _currentTime = currentTime;
                ReadyButtonClickedHandler();
                _view.SetReadyHoldProgress(0);
            }
        }

        public void MoveSelection(float horizontal, float vertical)
        {
            if (!_disposed && IsVisible)
            {
                _view.MoveSelection(horizontal, vertical);
            }
        }

        public void SubmitSelection()
        {
            if (!_disposed && IsVisible)
            {
                _view.SubmitSelection();
            }
        }

        public void LockSelectedOffer()
        {
            if (!_disposed && IsVisible && !IsPending && !_isShowingStats)
            {
                _view.LockSelectedOffer();
            }
        }

        public void MergeSelectedWeapon()
        {
            if (!_disposed && IsVisible && !IsPending && !_isShowingStats)
            {
                _view.MergeSelectedWeapon();
            }
        }

        public void ToggleStats()
        {
            ToggleStatsClickedHandler();
        }

        public void RerollShop()
        {
            if (!_disposed && IsVisible && !_isShowingStats)
            {
                if (_state.PendingUpgrades > 0)
                {
                    RerollUpgradesClickedHandler();
                }
                else
                {
                    RerollShopClickedHandler();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _view.OnBuyOfferClicked -= BuyOfferClickedHandler;
            _view.OnLockOfferClicked -= LockOfferClickedHandler;
            _view.OnSellWeaponClicked -= SellWeaponClickedHandler;
            _view.OnMergeWeaponClicked -= MergeWeaponClickedHandler;
            _view.OnUpgradeChoiceClicked -= UpgradeChoiceClickedHandler;
            _view.OnTakeItemRewardClicked -= TakeItemRewardClickedHandler;
            _view.OnRecycleItemRewardClicked -= RecycleItemRewardClickedHandler;
            _view.OnRerollShopClicked -= RerollShopClickedHandler;
            _view.OnRerollUpgradesClicked -= RerollUpgradesClickedHandler;
            _view.OnReadyButtonClicked -= ReadyButtonClickedHandler;
            _view.OnExitToMenuClicked -= ExitToMenuClickedHandler;
            _view.OnPreviousItemsPageClicked -= PreviousItemsPageClickedHandler;
            _view.OnNextItemsPageClicked -= NextItemsPageClickedHandler;
            _view.OnToggleStatsClicked -= ToggleStatsClickedHandler;
            _view.OnSelectionChanged -= SelectionChangedHandler;
            IsVisible = false;
            IsPending = false;
            _view.Render(new ShopViewData());
        }

        private void SelectionChangedHandler(string selectionKey)
        {
            _selectionKey = selectionKey;
            PublishNavigation();
        }

        private void PublishNavigation()
        {
            if (!_disposed && IsVisible && _state.CanControl)
            {
                _commands.TrySendNavigation(new ShopNavigationState
                {
                    SelectionKey = _selectionKey,
                    ItemPage = _itemPage,
                    IsShowingStats = _isShowingStats
                });
            }
        }

        private void BuyOfferClickedHandler(int offerSlot)
        {
            SendShopCommand(ShopCommandKind.Buy, offerSlot);
        }

        private void LockOfferClickedHandler(int offerSlot)
        {
            SendShopCommand(ShopCommandKind.Lock, offerSlot);
        }

        private void SellWeaponClickedHandler(int weaponSlot)
        {
            SendShopCommand(ShopCommandKind.Sell, weaponSlot);
        }

        private void MergeWeaponClickedHandler(int weaponSlot)
        {
            SendShopCommand(ShopCommandKind.Merge, weaponSlot);
        }

        private void UpgradeChoiceClickedHandler(int upgradeChoiceSlot)
        {
            SendShopCommand(ShopCommandKind.Upgrade, upgradeChoiceSlot);
        }

        private void TakeItemRewardClickedHandler()
        {
            SendShopCommand(ShopCommandKind.TakeItemReward, 0);
        }

        private void RecycleItemRewardClickedHandler()
        {
            SendShopCommand(ShopCommandKind.RecycleItemReward, 0);
        }

        private void RerollShopClickedHandler()
        {
            SendShopCommand(ShopCommandKind.Reroll, 0);
        }

        private void RerollUpgradesClickedHandler()
        {
            SendShopCommand(ShopCommandKind.RerollUpgrades, 0);
        }

        private void ReadyButtonClickedHandler()
        {
            SendShopCommand(ShopCommandKind.Ready, 0);
        }

        private void PreviousItemsPageClickedHandler()
        {
            ChangeItemsPage(-1);
        }

        private void NextItemsPageClickedHandler()
        {
            ChangeItemsPage(1);
        }

        private void ToggleStatsClickedHandler()
        {
            if (_disposed || !IsVisible || !_state.CanControl || !_usesPages || _state.IsRewardScreen
                || _state.PendingUpgrades > 0 || _state.PendingItemRewards.Length > 0)
            {
                return;
            }

            _isShowingStats = !_isShowingStats;
            Render();
        }

        private void SendShopCommand(ShopCommandKind command, int commandSlot)
        {
            if (_disposed || IsPending || !ShopActionAvailability.CanExecute(_state, command, commandSlot))
            {
                return;
            }

            if (!_commands.TrySend(command, commandSlot, _state.Revision))
            {
                _feedbackNotice = "Магазин или соединение изменились. Обновляем данные…";
                Render();
                return;
            }

            IsPending = true;
            _pendingRevision = _state.Revision;
            _commandSentAt = _currentTime;
            _feedbackNotice = "Ожидаем подтверждение сервера…";
            Render();
        }

        private void ExitToMenuClickedHandler()
        {
            if (!_disposed && IsVisible)
            {
                OnExitToMenuRequested?.Invoke();
            }
        }

        private void ChangeItemsPage(int direction)
        {
            if (_disposed || !IsVisible || !_state.CanControl || !_usesPages || _state.PendingUpgrades > 0)
            {
                return;
            }

            _itemPage += Math.Sign(direction);
            Render();
        }

        private void Render()
        {
            int itemPageCount = Math.Max(1, (_state.Items.Length + ShopViewDataBuilder.ITEMS_PER_PAGE - 1) / ShopViewDataBuilder.ITEMS_PER_PAGE);
            _itemPage = Math.Max(0, Math.Min(_itemPage, itemPageCount - 1));
            ShopViewData viewData = ShopViewDataBuilder.Build(_state, _itemPage, IsPending, _feedbackNotice, _isShowingStats, _usesPages);
            viewData.Navigation = _state.Navigation;
            _view.Render(viewData);
            PublishNavigation();
            _renderedState = _state;
            _isRenderRequired = false;
        }
    }
}
