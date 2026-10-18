using System;
using System.Collections.Generic;
using HordeMuncher.UI.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace HordeMuncher.UI.Shop.View
{
    public sealed class ShopView : IShopView, IDisposable
    {
        private const float DESIGN_WIDTH = 1200;
        private const float DESIGN_HEIGHT = 720;
        private const float DESCRIPTION_FONT_SIZE = 14;
        private const float TOOLTIP_WIDTH = 280;
        private const float TOOLTIP_MAX_HEIGHT = 360;
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
        private readonly ShopIconResolver _iconResolver;
        private readonly VisualElement _root;
        private readonly List<ShopViewButton> _buttons = new List<ShopViewButton>();
        private string _submitHint = "E / Enter / A";
        private string _mergeHint = "R / X";
        private int _weaponCount;
        private string _lockHint = "F / .";
        private string _rerollHint = "Shift+R / Shift+/";
        private string _statsHint = "Tab / RB";
        private string _readyHoldHint = "Shift+E / LT+A";
        private ProgressBar _readyHoldProgress;
        private float _readyHoldValue;
        private VisualElement _detailsArea;
        private VisualElement _selectedItemActions;
        private Label _detailsPlaceholder;
        private Label _tooltip;
        private int _selectedButtonIndex;
        private int _inspectionTileCount;
        private string _remoteSelectionKey;
        private bool _disposed;
        private bool _isCompact;
        private bool _canControl;
        private bool _isLeveling;
        public VisualElement Viewport { get; }

        public ShopView(VisualElement parent, ShopIconResolver iconResolver)
        {
            _iconResolver = iconResolver ?? throw new ArgumentNullException(nameof(iconResolver));
            Viewport = new VisualElement();
            Viewport.AddToClassList("shop-viewport");
            _root = new VisualElement();
            _root.AddToClassList("shop-panel");
            Viewport.Add(_root);
            parent.Add(Viewport);
            Viewport.style.display = DisplayStyle.None;
            Viewport.RegisterCallback<GeometryChangedEvent>(GeometryChangedHandler);
        }

        public void Render(ShopViewData viewData)
        {
            if (_disposed)
            {
                return;
            }

            Viewport.style.display = viewData.IsVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!viewData.IsVisible)
            {
                return;
            }

            bool wasRewardScreen = _root.ClassListContains("reward-screen");
            _root.EnableInClassList("reward-screen", viewData.IsRewardScreen);
            Viewport.EnableInClassList("reward-viewport", viewData.IsRewardScreen);
            Viewport.EnableInClassList("store-viewport", !viewData.IsRewardScreen);
            bool isItemReward = viewData.IsRewardScreen && !viewData.IsLeveling && viewData.Offers.Length > 0;
            _root.EnableInClassList("item-reward-screen", isItemReward);
            Viewport.EnableInClassList("item-reward-viewport", isItemReward);
            _root.EnableInClassList("reward-single-player", !viewData.UsesPages);
            _canControl = viewData.CanControl;
            _isLeveling = viewData.IsLeveling;
            _remoteSelectionKey = viewData.Navigation.SelectionKey;
            _inspectionTileCount = 0;
            _root.EnableInClassList("player-local", viewData.CanControl);
            for (int colorIndex = 0; colorIndex < 4; colorIndex++)
            {
                _root.EnableInClassList("player-color-" + colorIndex, colorIndex == (viewData.PlayerNumber - 1) % 4);
            }

            ShopViewButton previousSelection = _selectedButtonIndex >= 0 && _selectedButtonIndex < _buttons.Count
                ? _buttons[_selectedButtonIndex] : null;
            ShopButtonData previousCommand = previousSelection?.CommandData;
            string previousSelectionKey = previousSelection?.SelectionKey;
            ShopCardData previousWeapon = previousSelection?.WeaponCard;
            if (previousWeapon?.MergeResult != null && viewData.WeaponCount == _weaponCount - 1)
            {
                int upgradedSlot = previousWeapon.WeaponSlot - (previousWeapon.MergePartnerSlot < previousWeapon.WeaponSlot ? 1 : 0);
                if (Array.Exists(viewData.Weapons,
                    weapon => weapon.WeaponSlot == upgradedSlot && weapon.CatalogId == previousWeapon.MergeResult.CatalogId))
                {
                    previousCommand = null;
                    previousSelectionKey = "weapon:" + upgradedSlot;
                }
            }

            _weaponCount = viewData.WeaponCount;
            _root.Clear();
            _readyHoldProgress = null;
            AddLabel(_root, viewData.PlayerText, "shop-player-heading");
            _buttons.Clear();
            _tooltip = new Label
            {
                pickingMode = PickingMode.Ignore
            };
            _tooltip.AddToClassList("shop-tooltip");
            _tooltip.AddToClassList("hidden");
            if (viewData.IsRewardScreen)
            {
                RenderRewards(viewData);
                _selectedButtonIndex = 0;
                RestoreSelection(wasRewardScreen ? previousCommand : null);
                ApplyLayout();
                return;
            }

            if (wasRewardScreen)
            {
                previousCommand = null;
                previousSelectionKey = null;
                _selectedButtonIndex = 0;
            }

            VisualElement frame = AddContainer(_root, "shop-frame");
            frame.EnableInClassList("shop-single-player", !viewData.UsesPages && !viewData.IsShowingStats);
            VisualElement main = AddContainer(frame, "shop-main");
            main.EnableInClassList("level-main", viewData.IsLeveling);
            VisualElement header = AddContainer(main, "shop-header");
            AddLabel(header, viewData.IsShowingStats ? "Характеристики" : viewData.Title, "shop-title");
            if (!viewData.IsShowingStats)
            {
                AddMoney(header, viewData.MoneyText);
                AddCommandButton(header, viewData.Reroll);
            }

            if (viewData.CanControl && viewData.UsesPages)
            {
                AddLocalButton(header, "[" + _statsHint + "] " + (viewData.IsShowingStats ? "В магазин" : "Характеристики"),
                    "shop-stats-toggle", () => OnToggleStatsClicked?.Invoke());
            }

            VisualElement offersHost = AddContainer(main, "shop-offers-host");
            VisualElement lower = AddContainer(main, "shop-lower");
            VisualElement inventoryHost = AddContainer(lower, "shop-inventory-host");
            inventoryHost.EnableInClassList("hidden", viewData.IsShowingStats);
            VisualElement information = AddContainer(lower, "shop-information");
            _detailsArea = AddContainer(information, "shop-details");
            _detailsArea.EnableInClassList("hidden", viewData.IsShowingStats);
            _selectedItemActions = null;
            _detailsPlaceholder = new Label(viewData.IsLeveling ? "" : "Выберите предмет, чтобы посмотреть описание");
            _detailsPlaceholder.AddToClassList("shop-details-placeholder");
            _detailsArea.Add(_detailsPlaceholder);
            if (!viewData.IsShowingStats)
            {
                RenderOffers(offersHost, viewData);
                RenderInventory(inventoryHost, viewData);
            }

            if (viewData.IsShowingStats)
            {
                RenderStats(information, viewData);
            }
            else if (!viewData.UsesPages)
            {
                VisualElement statsSidebar = AddContainer(frame, "shop-stats-sidebar");
                AddLabel(statsSidebar, "Характеристики", "shop-section-title");
                RenderStats(statsSidebar, viewData);
            }

            if (!viewData.IsShowingStats)
            {
                VisualElement next = AddContainer(main, "shop-next-wave");
                AddLabel(next, viewData.NextWaveText, "shop-text");
                if (!viewData.IsLeveling)
                {
                    AddCommandButton(next, viewData.Ready);
                    if (viewData.CanControl)
                    {
                        _readyHoldProgress = new ProgressBar
                        {
                            lowValue = 0,
                            highValue = 1,
                            value = _readyHoldValue,
                            title = "Удерживайте " + _readyHoldHint + " · 1 сек",
                            pickingMode = PickingMode.Ignore
                        };
                        next.Add(_readyHoldProgress);
                    }
                }

                if (viewData.CanControl)
                {
                    AddLocalButton(next, "В меню", "shop-back", () => OnExitToMenuClicked?.Invoke());
                }

                AddLabel(_root, viewData.FeedbackText, "shop-result");
            }

            _root.Add(_tooltip);
            RestoreSelection(previousCommand, previousSelectionKey);
            ApplyLayout();
        }

        public void SetReadyHoldProgress(float progress)
        {
            _readyHoldValue = Mathf.Clamp01(progress);
            if (_readyHoldProgress != null)
            {
                _readyHoldProgress.value = _readyHoldValue;
            }
        }

        public void SetControlHints(string submitHint, string lockHint, string rerollHint,
            string statsHint = "Tab / RB", string readyHoldHint = "Shift+E / LT+A", string mergeHint = "R / X")
        {
            _submitHint = submitHint;
            _lockHint = lockHint;
            _rerollHint = rerollHint;
            _statsHint = statsHint;
            _readyHoldHint = readyHoldHint;
            _mergeHint = mergeHint;
        }

        public void SetPlayerLayout(int playerIndex, int playerCount)
        {
            int columns = playerCount > 1 ? 2 : 1;
            int rows = playerCount > 2 ? 2 : 1;
            Viewport.style.position = Position.Absolute;
            Viewport.style.left = Length.Percent(playerIndex % columns * 100f / columns);
            Viewport.style.top = Length.Percent(playerIndex / columns * 100f / rows);
            Viewport.style.width = Length.Percent(100f / columns);
            Viewport.style.height = Length.Percent(100f / rows);
            _isCompact = playerCount > 1;
            _root.EnableInClassList("shop-compact", _isCompact);
            ApplyLayout();
        }

        public void MoveSelection(float horizontal, float vertical)
        {
            if (_disposed || !_canControl || _buttons.Count == 0)
            {
                return;
            }

            if (_isLeveling)
            {
                if (vertical != 0)
                {
                    _selectedButtonIndex = Mathf.Clamp(_selectedButtonIndex + (vertical < 0 ? 1 : -1), 0, _buttons.Count - 1);
                    UpdateSelectedButtonHighlight();
                }

                return;
            }

            Vector2 direction = Mathf.Abs(horizontal) > Mathf.Abs(vertical)
                ? new Vector2(Mathf.Sign(horizontal), 0)
                : new Vector2(0, -Mathf.Sign(vertical));
            if (direction.x != 0 && TryMoveWithinItemActions(direction.x > 0 ? 1 : -1))
            {
                return;
            }

            ShopViewButton selected = _buttons[_selectedButtonIndex];
            Vector2 selectedButtonCenter = selected.Button.worldBound.center;
            bool isSelectingItem = selected.ItemTarget != null && !selected.ItemActions.Contains(selected.Button);
            int bestButtonIndex = -1;
            float bestNavigationScore = float.MaxValue;
            for (int buttonIndex = 0; buttonIndex < _buttons.Count; buttonIndex++)
            {
                if (buttonIndex == _selectedButtonIndex
                    || !IsButtonVisible(_buttons[buttonIndex].Button)
                    || isSelectingItem && direction.y != 0 && _buttons[buttonIndex].ItemActions != null
                    && _buttons[buttonIndex].ItemActions.Contains(_buttons[buttonIndex].Button))
                {
                    continue;
                }

                ShopViewButton candidate = _buttons[buttonIndex];
                Vector2 candidateCenter = candidate.Button.worldBound.center;
                Vector2 offset = candidateCenter - selectedButtonCenter;
                float forward = Vector2.Dot(offset, direction);
                if (forward <= 1)
                {
                    continue;
                }

                float sideways = Mathf.Abs(offset.x * direction.y - offset.y * direction.x);
                float navigationScore = forward + sideways * 3;
                if (navigationScore >= bestNavigationScore)
                {
                    continue;
                }

                bestNavigationScore = navigationScore;
                bestButtonIndex = buttonIndex;
            }

            if (bestButtonIndex < 0)
            {
                return;
            }

            _selectedButtonIndex = bestButtonIndex;
            UpdateSelectedButtonHighlight();
        }

        public void SubmitSelection()
        {
            if (!_disposed && _canControl && _buttons.Count > 0
                && IsButtonVisible(_buttons[_selectedButtonIndex].Button))
            {
                _buttons[_selectedButtonIndex].Invoke();
            }
        }

        public void LockSelectedOffer()
        {
            if (_disposed || !_canControl || _buttons.Count == 0)
            {
                return;
            }

            ShopButtonData selectedCommand = _buttons[_selectedButtonIndex].CommandData;
            if (selectedCommand == null || selectedCommand.Command != ShopCommandKind.Buy && selectedCommand.Command != ShopCommandKind.Lock)
            {
                return;
            }

            ShopViewButton lockButton = _buttons.Find(button => button.CommandData != null
                && button.CommandData.Command == ShopCommandKind.Lock && button.CommandData.Slot == selectedCommand.Slot);
            if (lockButton != null && lockButton.CommandData.Enabled)
            {
                OnLockOfferClicked?.Invoke(selectedCommand.Slot);
            }
        }

        public void MergeSelectedWeapon()
        {
            if (_disposed || !_canControl || _buttons.Count == 0)
            {
                return;
            }

            ShopViewButton selected = _buttons[_selectedButtonIndex];
            if (selected.WeaponCard == null)
            {
                return;
            }

            ShopViewButton mergeButton = _buttons.Find(button => button.ItemTarget == selected.ItemTarget
                && button.CommandData?.Command == ShopCommandKind.Merge);
            if (mergeButton != null && mergeButton.CommandData.Enabled)
            {
                OnMergeWeaponClicked?.Invoke(mergeButton.CommandData.Slot);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Viewport.UnregisterCallback<GeometryChangedEvent>(GeometryChangedHandler);
            Viewport.RemoveFromHierarchy();
            _buttons.Clear();
        }

        private void GeometryChangedHandler(GeometryChangedEvent change)
        {
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            float width = Viewport.contentRect.width;
            float height = Viewport.contentRect.height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            float designWidth = _isCompact ? 880 : DESIGN_WIDTH;
            float minimumHeight = _isCompact ? 600 : DESIGN_HEIGHT;
            float scale = Mathf.Max(0.01f, Mathf.Min((width - 12) / designWidth, (height - 12) / minimumHeight));
            float designHeight = (height - 12) / scale;
            _root.style.width = (width - 12) / scale;
            _root.style.height = designHeight;
            _root.style.transformOrigin = new TransformOrigin(0, 0, 0);
            _root.style.scale = new Scale(new Vector3(scale, scale, 1));
            _root.style.left = 6;
            _root.style.top = 6;
        }

        private void RenderRewards(ShopViewData viewData)
        {
            if (!viewData.IsLeveling && viewData.Offers.Length > 0)
            {
                RenderItemReward(viewData);
                return;
            }

            VisualElement screen = AddContainer(_root, "reward-content");
            AddLabel(screen, viewData.Title, "shop-title");
            VisualElement header = AddContainer(screen, "shop-header");
            AddMoney(header, viewData.MoneyText);
            if (viewData.IsLeveling)
            {
                AddCommandButton(header, viewData.Reroll);
            }

            if (viewData.Offers.Length == 0)
            {
                AddLabel(screen, "Награды выбраны. Ожидаем остальных игроков…", "shop-text");
            }

            _detailsArea = AddContainer(screen, "shop-details");
            _detailsArea.EnableInClassList("hidden", viewData.IsLeveling || viewData.Offers.Length == 0);
            _detailsPlaceholder = new Label("");
            _detailsArea.Add(_detailsPlaceholder);
            _selectedItemActions = null;
            RenderOffers(screen, viewData);
            _detailsArea.BringToFront();
            AddLabel(screen, viewData.NextWaveText, "shop-text");
            AddLabel(screen, viewData.FeedbackText, "shop-result");
            if (viewData.CanControl && !viewData.IsLeveling)
            {
                AddLocalButton(screen, "В меню", "shop-back", () => OnExitToMenuClicked?.Invoke());
            }

            _root.Add(_tooltip);
        }

        private void RenderItemReward(ShopViewData viewData)
        {
            ShopCardData item = viewData.Offers[0];
            VisualElement screen = AddContainer(_root, "item-reward-content");
            AddLabel(screen, viewData.Title, "item-reward-title");
            _detailsArea = AddContainer(screen, "item-reward-card");
            _detailsPlaceholder = new Label("");
            _selectedItemActions = null;
            VisualElement heading = AddContainer(_detailsArea, "item-reward-heading");
            AddProductIcon(heading, item, false);
            VisualElement names = AddContainer(heading, "item-reward-names");
            AddLabel(names, item.Name, "item-reward-name");
            AddLabel(names, item.Caption, "item-reward-caption");
            var description = new ScrollView(ScrollViewMode.Vertical);
            description.AddToClassList("item-reward-description");
            _detailsArea.Add(description);
            foreach (ShopStatChangeData change in item.StatChanges)
            {
                VisualElement row = AddContainer(description, "item-reward-stat");
                Image icon = ShopStatIcons.Create((int)change.Stat);
                row.Add(icon);
                row.EnableInClassList("item-reward-penalty", change.IsPenalty);
                AddLabel(row, change.Text, "item-reward-stat-text");
            }

            if (item.StatChanges.Length == 0)
            {
                AddLabel(description, item.Description, "item-reward-stat-text");
            }

            VisualElement actions = AddContainer(screen, "item-reward-actions");
            foreach (ShopButtonData button in item.Buttons)
            {
                AddCommandButton(actions, button);
            }

            if (!string.IsNullOrEmpty(viewData.FeedbackText))
            {
                AddLabel(screen, viewData.FeedbackText, "item-reward-feedback");
            }

            _root.Add(_tooltip);
        }

        private void RestoreSelection(ShopButtonData previousCommand, string previousSelectionKey = null)
        {
            int previousCommandIndex = previousCommand == null ? -1 : _buttons.FindIndex(button => button.CommandData != null
                && button.CommandData.Command == previousCommand.Command && button.CommandData.Slot == previousCommand.Slot);
            int previousSelectionIndex = string.IsNullOrEmpty(previousSelectionKey) ? -1
                : _buttons.FindIndex(button => button.SelectionKey == previousSelectionKey);
            _selectedButtonIndex = previousSelectionIndex >= 0 ? previousSelectionIndex
                : previousCommandIndex >= 0 ? previousCommandIndex
                : Mathf.Clamp(_selectedButtonIndex, 0, Mathf.Max(0, _buttons.Count - 1));
            if (!_canControl)
            {
                _selectedButtonIndex = _buttons.FindIndex(button => button.SelectionKey == _remoteSelectionKey);
            }

            UpdateSelectedButtonHighlight();
        }

        private void RenderOffers(VisualElement main, ShopViewData viewData)
        {
            VisualElement list = AddContainer(main, "shop-offers");
            foreach (ShopCardData cardData in viewData.Offers)
            {
                VisualElement card = AddContainer(list, "shop-offer-card");
                card.EnableInClassList("level-card", viewData.IsLeveling);
                card.EnableInClassList("shop-sold", cardData.IsSold);
                if (!string.IsNullOrEmpty(cardData.RarityStyle))
                {
                    card.AddToClassList(cardData.RarityStyle);
                }

                card.EnableInClassList("shop-weapon-card", cardData.IsWeapon);
                card.EnableInClassList("shop-offer-locked", cardData.IsLocked);
                VisualElement productIcon = AddProductIcon(card, cardData, false);
                VisualElement names = AddContainer(card, "shop-offer-names");
                AddLabel(names, cardData.Name, "shop-offer-name");
                if (!string.IsNullOrEmpty(cardData.Caption))
                {
                    AddLabel(names, cardData.Caption, "shop-caption");
                }
                if (cardData.IsLocked)
                {
                    productIcon.tooltip = "Закреплено: останется при обновлении магазина";
                    AddContainer(productIcon, "shop-lock-status").pickingMode = PickingMode.Ignore;
                }

                int firstButtonIndex = _buttons.Count;
                AddCommandButton(card, cardData.Buttons[0]);
                Button buy = _buttons[firstButtonIndex].Button;
                buy.EnableInClassList("shop-unaffordable", !cardData.IsAffordable);
                if (!viewData.IsRewardScreen)
                {
                    buy.Q<Label>(className: "shop-command-label").text = cardData.PriceText.Replace("●", "").Trim();
                }

                VisualElement actions = CreateItemActions();
                if (!viewData.IsLeveling)
                {
                    RenderProductInformation(actions, cardData);
                }
                for (int buttonIndex = 1; buttonIndex < cardData.Buttons.Length; buttonIndex++)
                {
                    AddCommandButton(card, cardData.Buttons[buttonIndex]);
                }

                if (cardData.IsSold)
                {
                    _buttons.RemoveRange(firstButtonIndex, _buttons.Count - firstButtonIndex);
                    continue;
                }

                BindItemActions(firstButtonIndex, card, actions);
            }
        }

        private void RenderProductInformation(VisualElement actions, ShopCardData cardData)
        {
            VisualElement heading = AddContainer(actions, "shop-detail-heading");
            AddProductIcon(heading, cardData, false);
            VisualElement names = AddContainer(heading, "shop-offer-names");
            AddLabel(names, cardData.Name + (string.IsNullOrEmpty(cardData.CountText) ? "" : " " + cardData.CountText), "shop-offer-name");
            AddLabel(names, cardData.Caption, "shop-caption");
            var description = new ScrollView(ScrollViewMode.Vertical);
            description.AddToClassList("shop-product-description");
            actions.Add(description);
            if (!string.IsNullOrEmpty(cardData.MergeNotice))
            {
                VisualElement preview = AddContainer(description, "shop-merge-preview");
                AddLabel(preview, cardData.MergeResult != null ? "Результат объединения" : "Объединение", "shop-merge-title");
                AddLabel(preview, cardData.MergeNotice, "shop-merge-notice");
                if (cardData.MergeResult != null)
                {
                    VisualElement resultHeading = AddContainer(preview, "shop-detail-heading");
                    AddProductIcon(resultHeading, cardData.MergeResult, false);
                    VisualElement resultNames = AddContainer(resultHeading, "shop-offer-names");
                    AddLabel(resultNames, cardData.MergeResult.Name, "shop-offer-name");
                    AddLabel(resultNames, cardData.MergeResult.Caption, "shop-caption");
                    AddLabel(preview, cardData.MergeResult.Description, "shop-context-description");
                    AddLabel(preview, "Два оружия займут один слот", "shop-caption");
                }
            }

            RenderStatChanges(description, cardData.StatChanges);
            if (cardData.StatChanges.Length == 0 || cardData.UpgradeStat.HasValue)
            {
                AddLabel(description, cardData.Description, "shop-context-description");
            }

            if (!string.IsNullOrEmpty(cardData.StatusText))
            {
                AddLabel(description, cardData.StatusText, "shop-offer-status");
            }
        }

        private void RenderStats(VisualElement parent, ShopViewData viewData)
        {
            var stats = new ScrollView(ScrollViewMode.Vertical);
            stats.AddToClassList("shop-stats");
            parent.Add(stats);
            AddLabel(stats, "Основное", "shop-stat-heading");
            foreach (ShopStatData stat in viewData.Stats)
            {
                VisualElement row = string.IsNullOrEmpty(stat.Tooltip) ? new VisualElement() : new Button { focusable = false };
                row.AddToClassList("shop-stat-row");
                stats.Add(row);
                row.EnableInClassList("shop-stat-improved", stat.Highlight);
                row.EnableInClassList("shop-stat-reduced", stat.IsPenalty);
                if (stat.Stat.HasValue)
                {
                    row.Add(ShopStatIcons.Create((int)stat.Stat.Value));
                }

                AddLabel(row, stat.Name, "shop-stat-name");
                AddLabel(row, stat.Value, stat.Highlight ? "shop-stat-bonus" : "shop-stat-value");
                BindStatInformation(row, stat.Tooltip);
                EnableTileInspection(row);
            }
        }

        private void RenderInventory(VisualElement main, ShopViewData viewData)
        {
            var inventory = new ScrollView(ScrollViewMode.Vertical);
            inventory.AddToClassList("shop-inventory");
            main.Add(inventory);
            VisualElement items = AddContainer(inventory, "shop-items-section");
            AddLabel(items, "Предметы", "shop-section-title");
            VisualElement itemGrid = AddContainer(items, "shop-icon-grid");
            foreach (ShopCardData item in viewData.Items)
            {
                VisualElement tile = AddProductIcon(itemGrid, item, true);
                int firstButtonIndex = _buttons.Count;
                EnableTileInspection(tile);
                VisualElement actions = CreateItemActions();
                RenderProductInformation(actions, item);
                BindItemActions(firstButtonIndex, tile, actions);
                if (!string.IsNullOrEmpty(item.CountText))
                {
                    AddLabel(tile, item.CountText, "shop-stack");
                }
            }

            if (viewData.Items.Length == 0)
            {
                AddLabel(itemGrid, "Пока нет предметов", "shop-caption");
            }

            if (viewData.ItemPages > 1)
            {
                VisualElement itemPageControls = AddContainer(items, "shop-pages");
                AddLocalButton(itemPageControls, "‹", "", () => OnPreviousItemsPageClicked?.Invoke());
                AddLabel(itemPageControls, (viewData.ItemPage + 1) + "/" + viewData.ItemPages, "shop-caption");
                AddLocalButton(itemPageControls, "›", "", () => OnNextItemsPageClicked?.Invoke());
            }

            VisualElement weapons = AddContainer(inventory, "shop-weapons-section");
            weapons.SendToBack();
            AddLabel(weapons, "Оружие (" + viewData.WeaponCount + "/" + viewData.WeaponSlots + ")", "shop-section-title");
            VisualElement weaponGrid = AddContainer(weapons, "shop-icon-grid");
            foreach (ShopCardData weapon in viewData.Weapons)
            {
                VisualElement holder = AddContainer(weaponGrid, "shop-weapon-slot");
                VisualElement tile = AddProductIcon(holder, weapon, true);
                tile.AddToClassList("shop-weapon-tile");
                int firstButtonIndex = _buttons.Count;
                EnableTileInspection(tile);
                _buttons[firstButtonIndex].SelectionKey = "weapon:" + weapon.WeaponSlot;
                VisualElement actions = CreateItemActions();
                RenderProductInformation(actions, weapon);
                foreach (ShopButtonData button in weapon.Buttons)
                {
                    AddCommandButton(actions, button);
                }

                BindItemActions(firstButtonIndex, tile, actions);
                for (int buttonIndex = firstButtonIndex; buttonIndex < _buttons.Count; buttonIndex++)
                {
                    _buttons[buttonIndex].WeaponCard = weapon;
                }
            }

            for (int emptyWeaponSlot = 0; emptyWeaponSlot < viewData.EmptyWeaponSlots; emptyWeaponSlot++)
            {
                VisualElement holder = AddContainer(weaponGrid, "shop-weapon-slot");
                AddLabel(holder, "+", "shop-empty-slot");
            }
        }

        private VisualElement AddProductIcon(VisualElement parent, ShopCardData cardData, bool tile)
        {
            VisualElement element = tile ? new Button
            {
                focusable = false
            }
                : new VisualElement();
            element.AddToClassList(tile ? "shop-icon-tile" : "shop-icon");
            if (!string.IsNullOrEmpty(cardData.RarityStyle))
            {
                element.AddToClassList(cardData.RarityStyle);
            }

            if (tile)
            {
                element.AddToClassList("shop-button");
            }

            parent.Add(element);
            if (cardData.UpgradeStat.HasValue)
            {
                Image statIcon = ShopStatIcons.Create((int)cardData.UpgradeStat.Value);
                statIcon.AddToClassList("shop-upgrade-icon");
                element.Add(statIcon);
            }
            else if (cardData.CatalogId >= 0 && _iconResolver(cardData.CatalogId, out Sprite sprite))
            {
                var image = new Image
                {
                    sprite = sprite,
                    scaleMode = ScaleMode.ScaleToFit
                };
                image.AddToClassList("shop-icon-image");
                element.Add(image);
            }
            else
            {
                string initials = string.IsNullOrEmpty(cardData.Name) ? "?" : cardData.Name.Substring(0,
                    Mathf.Min(2, cardData.Name.Length)).ToUpperInvariant();
                AddLabel(element, initials, "shop-icon-placeholder");
            }

            return element;
        }

        private static void RenderStatChanges(VisualElement parent, ShopStatChangeData[] changes)
        {
            foreach (ShopStatChangeData change in changes)
            {
                VisualElement row = AddContainer(parent, "shop-stat-change-row");
                row.Add(ShopStatIcons.Create((int)change.Stat));
                var label = new Label(change.Text);
                label.AddToClassList("shop-stat-change");
                label.AddToClassList(change.IsPenalty ? "shop-stat-penalty" : "shop-stat-positive");
                label.RegisterCallback<GeometryChangedEvent>(StatChangeGeometryChangedHandler);
                row.Add(label);
            }
        }

        private void NotifyButtonClicked(ShopButtonData button)
        {
            switch (button.Command)
            {
                case ShopCommandKind.TakeItemReward:
                    OnTakeItemRewardClicked?.Invoke();
                    break;
                case ShopCommandKind.RecycleItemReward:
                    OnRecycleItemRewardClicked?.Invoke();
                    break;
                case ShopCommandKind.Buy:
                    OnBuyOfferClicked?.Invoke(button.Slot);
                    break;
                case ShopCommandKind.Lock:
                    OnLockOfferClicked?.Invoke(button.Slot);
                    break;
                case ShopCommandKind.Sell:
                    OnSellWeaponClicked?.Invoke(button.Slot);
                    break;
                case ShopCommandKind.Merge:
                    OnMergeWeaponClicked?.Invoke(button.Slot);
                    break;
                case ShopCommandKind.Upgrade:
                    OnUpgradeChoiceClicked?.Invoke(button.Slot);
                    break;
                case ShopCommandKind.Reroll:
                    OnRerollShopClicked?.Invoke();
                    break;
                case ShopCommandKind.RerollUpgrades:
                    OnRerollUpgradesClicked?.Invoke();
                    break;
                case ShopCommandKind.Ready:
                    OnReadyButtonClicked?.Invoke();
                    break;
            }
        }

        private static void AddMoney(VisualElement parent, string text)
        {
            VisualElement money = AddContainer(parent, "shop-money");
            AddCoinIcon(money);
            AddLabel(money, text.Replace("●", "").Trim(), "shop-money-value");
        }

        private static void AddCoinIcon(VisualElement parent)
        {
            VisualElement coin = AddContainer(parent, "battle-coin");
            coin.pickingMode = PickingMode.Ignore;
            AddContainer(coin, "battle-coin-back").pickingMode = PickingMode.Ignore;
            VisualElement rim = AddContainer(coin, "battle-coin-rim");
            rim.pickingMode = PickingMode.Ignore;
            AddContainer(rim, "battle-coin-mark").pickingMode = PickingMode.Ignore;
        }

        private void AddCommandButton(VisualElement parent, ShopButtonData buttonData)
        {
            var button = new Button
            {
                focusable = false
            };
            Action invoke = () =>
            {
                SelectButton(button);
                if (_canControl && buttonData.Enabled)
                {
                    NotifyButtonClicked(buttonData);
                }
            };
            button.RegisterCallback<PointerEnterEvent>(_ => SelectButton(button));
            button.clicked += invoke;
            button.AddToClassList("shop-button");
            foreach (string style in buttonData.Style.Split(' '))
            {
                button.AddToClassList(style);
            }

            string shortcut = buttonData.Command == ShopCommandKind.Merge ? _mergeHint
                : buttonData.Command == ShopCommandKind.Lock && !string.IsNullOrEmpty(_lockHint) ? _lockHint
                : buttonData.Command == ShopCommandKind.Reroll || buttonData.Command == ShopCommandKind.RerollUpgrades
                    ? _rerollHint : _submitHint;
            if (_canControl && !string.IsNullOrEmpty(shortcut))
            {
                button.AddToClassList("shop-command-button");
                var keyHint = new Label(shortcut)
                {
                    pickingMode = PickingMode.Ignore
                };
                keyHint.AddToClassList("shop-key-hint");
                button.Add(keyHint);
            }

            var commandLabel = new Label(buttonData.Text.Replace("●", "").Trim())
            {
                pickingMode = PickingMode.Ignore
            };
            commandLabel.AddToClassList("shop-command-label");
            button.Add(commandLabel);
            if (buttonData.Text.Contains("●"))
            {
                button.AddToClassList("shop-command-button");
                AddCoinIcon(button);
            }
            button.EnableInClassList("shop-action-unavailable", !buttonData.Enabled);
            parent.Add(button);
            _buttons.Add(new ShopViewButton
            {
                Button = button,
                Invoke = invoke,
                SelectionKey = "command:" + buttonData.Command + ":" + buttonData.Slot,
                CommandData = buttonData
            });
        }

        private void AddLocalButton(VisualElement parent, string text, string style, Action invoke)
        {
            var button = new Button
            {
                text = text,
                focusable = false
            };
            Action click = () =>
            {
                SelectButton(button);
                invoke();
            };
            button.RegisterCallback<PointerEnterEvent>(_ => SelectButton(button));
            button.clicked += click;
            button.AddToClassList("shop-button");
            if (!string.IsNullOrEmpty(style))
            {
                button.AddToClassList(style);
            }

            parent.Add(button);
            _buttons.Add(new ShopViewButton
            {
                Button = button,
                SelectionKey = "local:" + (string.IsNullOrEmpty(style) ? text : style),
                Invoke = click
            });
        }

        private void EnableTileInspection(VisualElement tile)
        {
            if (tile is not Button button)
            {
                return;
            }

            void TileClickedHandler()
            {
                SelectButton(button);
            }

            button.RegisterCallback<PointerEnterEvent>(_ => SelectButton(button));
            button.clicked += TileClickedHandler;
            _buttons.Add(new ShopViewButton
            {
                Button = button,
                SelectionKey = "inspect:" + _inspectionTileCount++,
                Invoke = TileClickedHandler
            });
        }

        private void BindStatInformation(VisualElement target, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            target.userData = text;
            target.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(target, text));
            target.RegisterCallback<PointerLeaveEvent>(_ => _tooltip.AddToClassList("hidden"));
            target.RegisterCallback<ClickEvent>(_ => ShowTooltip(target, text));
        }

        private void ShowTooltip(VisualElement target, string text)
        {
            _tooltip.text = text.Replace("\n", "\n\n");
            _tooltip.RemoveFromClassList("hidden");
            PositionStatTooltip(target);
            _tooltip.schedule.Execute(() => PositionStatTooltip(target));
        }

        private void PositionStatTooltip(VisualElement target)
        {
            Vector2 point = _root.WorldToLocal(new Vector2(target.worldBound.xMin, target.worldBound.yMax));
            float width = _root.resolvedStyle.width;
            float height = _root.resolvedStyle.height;
            float left = point.x + 8;
            float tooltipHeight = _tooltip.layout.height;
            if (float.IsNaN(tooltipHeight) || tooltipHeight <= 0)
            {
                tooltipHeight = TOOLTIP_MAX_HEIGHT;
            }

            float top = point.y + 6;
            if (top + tooltipHeight > height - 4)
            {
                top = _root.WorldToLocal(target.worldBound.position).y - tooltipHeight - 6;
            }

            _tooltip.style.left = Mathf.Clamp(left, 4, Mathf.Max(4, width - TOOLTIP_WIDTH - 4));
            _tooltip.style.top = Mathf.Clamp(top, 4, Mathf.Max(4, height - tooltipHeight - 4));
        }

        private void UpdateSelectedButtonHighlight()
        {
            for (int buttonIndex = 0; buttonIndex < _buttons.Count; buttonIndex++)
            {
                _buttons[buttonIndex].Button.EnableInClassList("shop-selected", buttonIndex == _selectedButtonIndex);
            }

            if (_selectedButtonIndex < 0 || _selectedButtonIndex >= _buttons.Count)
            {
                return;
            }

            ShopViewButton selected = _buttons[_selectedButtonIndex];
            if (_canControl)
            {
                OnSelectionChanged?.Invoke(selected.SelectionKey);
            }

            if (selected.ItemActions != null)
            {
                _selectedItemActions = selected.ItemActions;
            }

            _detailsPlaceholder.EnableInClassList("hidden", _selectedItemActions != null);
            _detailsArea.Query<VisualElement>(className: "shop-context-actions").ForEach(actions =>
                actions.EnableInClassList("shop-context-visible", actions == _selectedItemActions));
            _root.Query<VisualElement>(className: "shop-offer-card").ForEach(card =>
                card.EnableInClassList("shop-item-selected", card == selected.ItemTarget));
            _root.Query<VisualElement>(className: "level-card").ForEach(card =>
                card.EnableInClassList("shop-item-selected", card == selected.ItemTarget));
            ShopCardData selectedWeapon = _buttons.Find(button => button.ItemActions == _selectedItemActions)?.WeaponCard;
            foreach (ShopViewButton weaponButton in _buttons)
            {
                if (weaponButton.WeaponCard == null || weaponButton.Button != weaponButton.ItemTarget)
                {
                    continue;
                }

                bool isMergeSource = selectedWeapon?.MergeResult != null
                    && (weaponButton.WeaponCard.WeaponSlot == selectedWeapon.WeaponSlot
                        || weaponButton.WeaponCard.WeaponSlot == selectedWeapon.MergePartnerSlot);
                weaponButton.Button.EnableInClassList("shop-merge-source", isMergeSource);
                weaponButton.Button.EnableInClassList("shop-weapon-inspected", weaponButton.ItemActions == _selectedItemActions);
            }
            _tooltip.AddToClassList("hidden");
            VisualElement informationTarget = _buttons[_selectedButtonIndex].Button;
            while (informationTarget != _root && string.IsNullOrEmpty(informationTarget.userData as string))
            {
                informationTarget = informationTarget.parent;
            }

            if (informationTarget.userData is string information)
            {
                ShowTooltip(informationTarget, information);
            }

            VisualElement ancestor = selected.Button.parent;
            while (ancestor != null && ancestor != _root)
            {
                if (ancestor is ScrollView scroll)
                {
                    scroll.ScrollTo(selected.Button);
                    break;
                }

                ancestor = ancestor.parent;
            }
        }

        private bool TryMoveWithinItemActions(int step)
        {
            VisualElement selectedTarget = _buttons[_selectedButtonIndex].ItemTarget;
            if (selectedTarget == null)
            {
                return false;
            }

            for (int buttonIndex = _selectedButtonIndex + step; buttonIndex >= 0 && buttonIndex < _buttons.Count; buttonIndex += step)
            {
                ShopViewButton candidate = _buttons[buttonIndex];
                if (candidate.ItemTarget != selectedTarget)
                {
                    break;
                }

                if (IsButtonVisible(candidate.Button))
                {
                    _selectedButtonIndex = buttonIndex;
                    UpdateSelectedButtonHighlight();
                    return true;
                }
            }

            return false;
        }

        private VisualElement CreateItemActions()
        {
            return AddContainer(_detailsArea, "shop-context-actions");
        }

        private void BindItemActions(int firstButtonIndex, VisualElement target, VisualElement actions)
        {
            for (int buttonIndex = firstButtonIndex; buttonIndex < _buttons.Count; buttonIndex++)
            {
                _buttons[buttonIndex].ItemTarget = target;
                _buttons[buttonIndex].ItemActions = actions;
            }

            target.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (_canControl)
                {
                    int selectableIndex = _buttons.FindIndex(button => button.ItemTarget == target);
                    if (selectableIndex >= 0)
                    {
                        SelectButton(_buttons[selectableIndex].Button);
                    }
                }
            });
        }

        private bool IsButtonVisible(Button button)
        {
            for (VisualElement element = button; element != null && element != _root; element = element.parent)
            {
                if (element.resolvedStyle.display == DisplayStyle.None || element.resolvedStyle.visibility == Visibility.Hidden)
                {
                    return false;
                }
            }

            return true;
        }

        private void SelectButton(Button button)
        {
            if (!_canControl)
            {
                return;
            }

            int buttonIndex = _buttons.FindIndex(candidate => candidate.Button == button);
            if (buttonIndex < 0)
            {
                return;
            }

            _selectedButtonIndex = buttonIndex;
            UpdateSelectedButtonHighlight();
        }

        private static void StatChangeGeometryChangedHandler(GeometryChangedEvent change)
        {
            var label = (Label)change.target;
            float availableWidth = label.contentRect.width - 1;
            float currentFontSize = label.resolvedStyle.fontSize;
            if (availableWidth <= 0 || currentFontSize <= 0)
            {
                return;
            }

            float textWidth = label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined,
                0, VisualElement.MeasureMode.Undefined).x;
            if (textWidth <= 0)
            {
                return;
            }

            float fontSize = Mathf.Min(DESCRIPTION_FONT_SIZE, currentFontSize * availableWidth / textWidth);
            if (Mathf.Abs(fontSize - currentFontSize) > 0.1f)
            {
                label.style.fontSize = fontSize;
            }
        }

        private static VisualElement AddContainer(VisualElement parent, string style)
        {
            var element = new VisualElement();
            element.AddToClassList(style);
            parent.Add(element);
            return element;
        }

        private static void AddLabel(VisualElement parent, string text, string style)
        {
            var label = new Label(text);
            label.AddToClassList(style);
            parent.Add(label);
        }
    }
}
