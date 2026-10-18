using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HordeMuncher.UI.Contracts;
using HordeMuncher.UI.Shop.Presentation;
using HordeMuncher.UI.Shop.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;

namespace HordeMuncher.Tests.Shop
{
    public sealed class ShopViewTests
    {
        [Test]
        public void ObserverFollowsOwnerSelectionAndStatsWithoutControllingShop()
        {
            var window = ScriptableObject.CreateInstance<UnityEditor.EditorWindow>();
            try
            {
                window.Show();
                var ownerModel = new FakeShopModel();
                var observerModel = new FakeShopModel();
                observerModel.State.CanControl = false;
                var ownerParent = new VisualElement();
                var observerParent = new VisualElement();
                window.rootVisualElement.Add(ownerParent);
                window.rootVisualElement.Add(observerParent);
                using var ownerView = new ShopView(ownerParent, TryGetIcon);
                using var observerView = new ShopView(observerParent, TryGetIcon);
                using var ownerPresenter = new ShopPresenter(ownerModel, ownerModel, ownerView);
                using var observerPresenter = new ShopPresenter(observerModel, observerModel, observerView);
                ownerPresenter.SetPlayerCount(2);
                observerPresenter.SetPlayerCount(2);
                ownerPresenter.Refresh(0);
                SendPointerEnter(ownerParent.Query<Button>(className: "shop-buy").ToList()[1]);
                observerModel.State.Navigation = ownerModel.Navigation;
                observerPresenter.Refresh(0);
                VisualElement selectedOffer = observerParent.Q<VisualElement>(className: "shop-item-selected");
                Assert.That(selectedOffer, Is.Not.Null);
                Assert.That(selectedOffer.Q<Label>(className: "shop-offer-name").text, Is.EqualTo("Heart"));
                observerPresenter.SubmitSelection();
                Assert.That(observerModel.Sent, Is.Empty);
                SendPointerEnter(ownerParent.Query<Button>(className: "shop-icon-tile").ToList()[0]);
                observerModel.State.Navigation = ownerModel.Navigation;
                observerPresenter.Refresh(0.5);
                Assert.That(observerParent.Q<Button>(className: "shop-selected").ClassListContains("shop-icon-tile"), Is.True);
                ownerPresenter.ToggleStats();
                observerModel.State.Navigation = ownerModel.Navigation;
                observerPresenter.Refresh(1);
                Assert.That(observerParent.Q<ScrollView>(className: "shop-stats"), Is.Not.Null);
                Assert.That(observerParent.Q<VisualElement>(className: "shop-offer-card"), Is.Null);
                ownerPresenter.ToggleStats();
                observerModel.State.Navigation = ownerModel.Navigation;
                observerPresenter.Refresh(2);
                Assert.That(observerParent.Q<ScrollView>(className: "shop-stats"), Is.Null);
                Assert.That(observerParent.Q<VisualElement>(className: "shop-offer-card"), Is.Not.Null);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void ItemTradeoffUsesStatDataForIconsAndPenaltyColor()
        {
            ShopState state = FakeShopModel.CreateState();
            ShopProduct item = state.Catalog[1];
            item.Description = "Произвольное описание";
            item.Bonus = ShopBonusKind.MeleeDamage;
            item.Amount = 6;
            item.Penalty = ShopBonusKind.AttackSpeed;
            item.PenaltyAmount = -0.12f;
            state.Catalog[1] = item;
            ShopViewData data = ShopViewDataBuilder.Build(state, 0, false, "");
            ShopStatChangeData[] changes = data.Offers[1].StatChanges;
            Assert.That(changes.Select(change => change.Stat),
                Is.EqualTo(new[] { ShopBonusKind.MeleeDamage, ShopBonusKind.AttackSpeed }));
            Assert.That(changes[1].Text, Is.EqualTo("−12 Скорость атаки"));
            var parent = new VisualElement();
            using var view = new ShopView(parent, TryGetIcon);
            view.Render(data);
            VisualElement card = parent.Query<VisualElement>(className: "shop-offer-card").ToList()[1];
            Assert.That(card.Query<Image>(className: "shop-stat-icon").ToList(), Is.Empty);
            VisualElement details = parent.Q<VisualElement>(className: "shop-details");
            VisualElement itemDetails = details.Query<VisualElement>(className: "shop-context-actions").ToList()[1];
            Assert.That(itemDetails.Query<Image>(className: "shop-stat-icon").ToList().Count, Is.EqualTo(2));
            Assert.That(itemDetails.Q<Label>(className: "shop-stat-penalty").text, Is.EqualTo(changes[1].Text));
        }

        [Test]
        public void EveryStatHasItsOwnAtlasCellAndUpgradeIcon()
        {
            ShopState state = FakeShopModel.CreateState();
            state.PendingUpgrades = 1;
            foreach (ShopBonusKind stat in Enum.GetValues(typeof(ShopBonusKind)))
            {
                state.UpgradeChoices = new[] { new ShopUpgradeChoice { Stat = stat, Points = 5 } };
                ShopViewData data = ShopViewDataBuilder.Build(state, 0, false, "");
                Assert.That(data.Stats.Count(row => row.Stat == stat), Is.EqualTo(1));
                var parent = new VisualElement();
                using var view = new ShopView(parent, TryGetIcon);
                view.Render(data);
                Image icon = parent.Q<Image>(className: "shop-upgrade-icon");
                Assert.That(icon.image, Is.Not.Null);
                Assert.That(icon.uv, Is.EqualTo(new Rect((int)stat % 4 / 4f, (3 - (int)stat / 4) / 4f, 0.25f, 0.25f)));
            }
        }

        [Test]
        public void PassiveViewRendersPresenterDataAndRoutesNavigationBackThroughEvents()
        {
            var parent = new VisualElement();
            var model = new FakeShopModel();
            using var view = new ShopView(parent, TryGetIcon);
            using var presenter = new ShopPresenter(model, model, view);
            presenter.Refresh(0);
            Assert.That(parent.Q<Label>(className: "shop-money-value").text, Is.EqualTo("100"));
            Assert.That(parent.Q<VisualElement>(className: "shop-money").Q<VisualElement>(className: "battle-coin"), Is.Not.Null);
            Assert.That(parent.Q<ScrollView>(className: "shop-stats"), Is.Not.Null);
            Assert.That(parent.Q<Button>(className: "shop-stats-toggle"), Is.Null);
            Assert.That(parent.Query<VisualElement>(className: "shop-offer-card").ToList().Count, Is.EqualTo(2));
            presenter.SubmitSelection();
            Assert.That(model.Sent.Count, Is.EqualTo(1));
            Assert.That(model.Sent[0].Command, Is.EqualTo(ShopCommandKind.Reroll));
            presenter.SubmitSelection();
            Assert.That(model.Sent.Count, Is.EqualTo(1));
            view.Dispose();
            Assert.That(parent.childCount, Is.Zero);
        }

        [Test]
        public void TwoPlayerLayoutKeepsShopAndStatsOnSeparatePages()
        {
            var parent = new VisualElement();
            var model = new FakeShopModel();
            using var view = new ShopView(parent, TryGetIcon);
            using var presenter = new ShopPresenter(model, model, view);
            presenter.SetPlayerCount(2);
            presenter.Refresh(0);
            Assert.That(parent.Q<ScrollView>(className: "shop-stats"), Is.Null);
            Assert.That(parent.Q<Button>(className: "shop-stats-toggle"), Is.Not.Null);

            presenter.ToggleStats();

            Assert.That(parent.Q<ScrollView>(className: "shop-stats"), Is.Not.Null);
            Assert.That(parent.Q<VisualElement>(className: "shop-offer-card"), Is.Null);
            Assert.That(parent.Q<VisualElement>(className: "shop-stats-sidebar"), Is.Null);
        }

        [Test]
        public void UnavailableSelectionStaysSelectedAfterRefreshWithoutExecuting()
        {
            var parent = new VisualElement();
            using var view = new ShopView(parent, TryGetIcon);
            ShopViewData viewData = ShopViewDataBuilder.Build(FakeShopModel.CreateState(), 0, false, "");
            view.Render(viewData);
            viewData.Reroll.Enabled = false;
            int rerollCount = 0;
            view.OnRerollShopClicked += () => rerollCount++;

            view.Render(viewData);
            view.SubmitSelection();

            Assert.That(rerollCount, Is.Zero);
            Assert.That(parent.Q<Button>(className: "shop-reroll").ClassListContains("shop-selected"), Is.True);
            Assert.That(parent.Q<Button>(className: "shop-reroll").ClassListContains("shop-action-unavailable"), Is.True);
        }

        [Test]
        public void ViewDoesNotStoreGameSimulationOrNetworkingState()
        {
            string[] fieldAssemblies = typeof(ShopView)
                .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(field => field.FieldType.Assembly.GetName().Name)
                .ToArray();
            Assert.That(fieldAssemblies, Does.Not.Contain("Unity.Entities"));
            Assert.That(fieldAssemblies, Does.Not.Contain("Unity.NetCode"));
            Assert.That(fieldAssemblies, Does.Not.Contain("HordeMuncher.Runtime"));
        }

        [TestCase(nameof(IShopView.OnBuyOfferClicked), ShopCommandKind.Buy, 7)]
        [TestCase(nameof(IShopView.OnLockOfferClicked), ShopCommandKind.Lock, 7)]
        [TestCase(nameof(IShopView.OnSellWeaponClicked), ShopCommandKind.Sell, 7)]
        [TestCase(nameof(IShopView.OnMergeWeaponClicked), ShopCommandKind.Merge, 7)]
        [TestCase(nameof(IShopView.OnUpgradeChoiceClicked), ShopCommandKind.Upgrade, 7)]
        [TestCase(nameof(IShopView.OnRerollShopClicked), ShopCommandKind.Reroll, 0)]
        [TestCase(nameof(IShopView.OnRerollUpgradesClicked), ShopCommandKind.RerollUpgrades, 0)]
        [TestCase(nameof(IShopView.OnReadyButtonClicked), ShopCommandKind.Ready, 0)]
        public void ViewRaisesTheSpecificButtonEventWithItsSlot(string eventName, ShopCommandKind command, int expectedSlot)
        {
            var parent = new VisualElement();
            using var view = new ShopView(parent, TryGetIcon);
            ShopViewData viewData = ShopViewDataBuilder.Build(FakeShopModel.CreateState(), 0, false, "");
            viewData.Reroll = new ShopButtonData
            {
                Text = "Test shop action",
                Style = "shop-reroll",
                Command = command,
                Slot = 7,
                Enabled = true
            };
            int actualSlot = -1;
            int clickCount = 0;
            EventInfo clickEvent = typeof(ShopView).GetEvent(eventName);
            Delegate callback;
            if (clickEvent.EventHandlerType == typeof(Action<int>))
            {
                callback = new Action<int>(clickedSlot =>
                    {
                        actualSlot = clickedSlot;
                        clickCount++;
                    });
            }
            else
            {
                callback = new Action(() =>
                    {
                        actualSlot = 0;
                        clickCount++;
                    });
            }

            clickEvent.AddEventHandler(view, callback);
            view.Render(viewData);
            view.SubmitSelection();
            Assert.That(clickCount, Is.EqualTo(1));
            Assert.That(actualSlot, Is.EqualTo(expectedSlot));
            clickEvent.RemoveEventHandler(view, callback);
        }

        [Test]
        public void StatHoverShowsAdjacentInformationAndLeavingHidesIt()
        {
            var window = ScriptableObject.CreateInstance<UnityEditor.EditorWindow>();
            try
            {
                window.Show();
                VerifyStatInformation(window.rootVisualElement);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void SelectedItemShowsItsOverlayAndHighlightsTheChosenActionForOnlyItsPlayer()
        {
            var window = ScriptableObject.CreateInstance<UnityEditor.EditorWindow>();
            try
            {
                window.Show();
                using var firstView = new ShopView(window.rootVisualElement, TryGetIcon);
                using var secondView = new ShopView(window.rootVisualElement, TryGetIcon);
                ShopViewData data = ShopViewDataBuilder.Build(FakeShopModel.CreateState(), 0, false, "");
                firstView.SetPlayerLayout(0, 2);
                secondView.SetPlayerLayout(1, 2);
                firstView.Render(data);
                secondView.Render(data);
                Assert.That(firstView.Viewport.Query<VisualElement>(className: "shop-context-visible").ToList(), Is.Empty);

                Button buy = firstView.Viewport.Query<Button>(className: "shop-buy").ToList()[0];
                SendPointerEnter(buy);
                VisualElement actions = firstView.Viewport.Query<VisualElement>(className: "shop-context-visible").ToList().Single();
                Assert.That(actions.parent, Is.SameAs(firstView.Viewport.Q<VisualElement>(className: "shop-details")));
                Assert.That(buy.ClassListContains("shop-selected"), Is.True);
                Assert.That(secondView.Viewport.Query<VisualElement>(className: "shop-context-visible").ToList(), Is.Empty);

                Button lockButton = buy.parent.Q<Button>(className: "shop-lock");
                SendPointerEnter(lockButton);
                int lockedOfferSlot = -1;
                firstView.OnLockOfferClicked += offerSlot => lockedOfferSlot = offerSlot;
                firstView.SubmitSelection();
                Assert.That(lockedOfferSlot, Is.EqualTo(data.Offers[0].Buttons[1].Slot));
                Assert.That(lockButton.ClassListContains("shop-selected"), Is.True);
                Assert.That(buy.ClassListContains("shop-selected"), Is.False);
                Assert.That(actions.ClassListContains("shop-context-visible"), Is.True);

                firstView.Render(data);
                Assert.That(firstView.Viewport.Query<Button>(className: "shop-selected").ToList().Single()
                    .ClassListContains("shop-lock"), Is.True);
                Assert.That(firstView.Viewport.Query<VisualElement>(className: "shop-context-visible").ToList().Count, Is.EqualTo(1));
                SendPointerEnter(firstView.Viewport.Q<Button>(className: "shop-reroll"));
                Assert.That(firstView.Viewport.Query<VisualElement>(className: "shop-context-visible").ToList().Count, Is.EqualTo(1));
            }
            finally
            {
                window.Close();
            }
        }

        [UnityTest]
        public IEnumerator PlayerPanelsFillTheirViewportAndKeepItemActionsInsideItAfterResizing()
        {
            var window = ScriptableObject.CreateInstance<UnityEditor.EditorWindow>();
            try
            {
                window.position = new Rect(50, 50, 1200, 720);
                window.Show();
                VisualElement parent = window.rootVisualElement;
                parent.styleSheets.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Project/UI/ArenaMenu.uss"));
                using var view = new ShopView(parent, TryGetIcon);
                ShopViewData data = ShopViewDataBuilder.Build(FakeShopModel.CreateState(), 0, false, "");
                view.Render(data);
                data.Offers = data.Offers.Concat(data.Offers).ToArray();
                view.Render(data);
                foreach (int playerCount in new[] { 1, 2, 3, 4 })
                {
                    view.SetPlayerLayout(playerCount - 1, playerCount);
                    yield return null;
                    yield return null;
                    VisualElement panel = view.Viewport.Q<VisualElement>(className: "shop-panel");
                    Rect viewportBounds = view.Viewport.worldBound;
                    Assert.That(panel.worldBound.width, Is.EqualTo(viewportBounds.width - 12).Within(2));
                    Assert.That(panel.worldBound.height, Is.EqualTo(viewportBounds.height - 12).Within(2));
                    Button lastOffer = view.Viewport.Query<Button>(className: "shop-buy").ToList().Last();
                    SendPointerEnter(lastOffer);
                    yield return null;
                    yield return null;
                    VisualElement actions = view.Viewport.Query<VisualElement>(className: "shop-context-visible").ToList().Single();
                    VisualElement offers = view.Viewport.Q<VisualElement>(className: "shop-offers");
                    Assert.That(actions.worldBound.yMin, Is.GreaterThanOrEqualTo(offers.worldBound.yMax));
                    Assert.That(lastOffer.worldBound.yMin, Is.GreaterThan(
                        view.Viewport.Query<Button>(className: "shop-buy").ToList().First().worldBound.yMax));
                    Assert.That(actions.worldBound.xMin, Is.GreaterThanOrEqualTo(viewportBounds.xMin));
                    Assert.That(actions.worldBound.xMax, Is.LessThanOrEqualTo(viewportBounds.xMax));
                    Assert.That(actions.worldBound.yMin, Is.GreaterThanOrEqualTo(viewportBounds.yMin));
                    Assert.That(actions.worldBound.yMax, Is.LessThanOrEqualTo(viewportBounds.yMax));
                    SendPointerEnter(lastOffer);
                    Assert.That(lastOffer.ClassListContains("shop-selected"), Is.True);
                    view.MoveSelection(1, 0);
                    Assert.That(lastOffer.parent.Q<Button>(className: "shop-lock").ClassListContains("shop-selected"), Is.True);
                    view.MoveSelection(-1, 0);
                    Assert.That(lastOffer.ClassListContains("shop-selected"), Is.True);
                    Assert.That(view.Viewport.Q<VisualElement>(className: "shop-frame").worldBound.yMax,
                        Is.LessThanOrEqualTo(panel.worldBound.yMax + 1));
                }
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void StatsReplaceShopAndReturningRestoresOffersAndInventory()
        {
            var parent = new VisualElement();
            var model = new FakeShopModel();
            using var view = new ShopView(parent, TryGetIcon);
            using var presenter = new ShopPresenter(model, model, view);
            presenter.SetPlayerCount(2);
            presenter.Refresh(0);
            presenter.ToggleStats();

            Assert.That(parent.Q<ScrollView>(className: "shop-stats"), Is.Not.Null);
            Assert.That(parent.Q<VisualElement>(className: "shop-offer-card"), Is.Null);
            Assert.That(parent.Q<ScrollView>(className: "shop-inventory"), Is.Null);
            Assert.That(parent.Q<Button>(className: "shop-reroll"), Is.Null);
            Assert.That(parent.Q<VisualElement>(className: "shop-next-wave"), Is.Null);
            string[] statNames = parent.Query<Label>(className: "shop-stat-name").ToList().Select(label => label.text).ToArray();
            int healthIndex = Array.IndexOf(statNames, ShopStatFormatter.GetName(ShopBonusKind.Health));
            Assert.That(statNames[healthIndex + 1], Is.EqualTo(ShopStatFormatter.GetName(ShopBonusKind.Regeneration)));
            presenter.RerollShop();
            presenter.LockSelectedOffer();
            Assert.That(model.Sent, Is.Empty);

            presenter.ToggleStats();

            Assert.That(parent.Q<ScrollView>(className: "shop-stats"), Is.Null);
            Assert.That(parent.Query<VisualElement>(className: "shop-offer-card").ToList().Count, Is.EqualTo(2));
            Assert.That(parent.Q<ScrollView>(className: "shop-inventory"), Is.Not.Null);
            Assert.That(parent.Q<Button>(className: "shop-reroll"), Is.Not.Null);
            Assert.That(parent.Q<VisualElement>(className: "shop-next-wave"), Is.Not.Null);
        }

        [Test]
        public void OfferHasVisibleLockActionAndConfirmedLockCanBeReleased()
        {
            var window = ScriptableObject.CreateInstance<UnityEditor.EditorWindow>();
            try
            {
                window.Show();
                VisualElement parent = window.rootVisualElement;
                using var view = new ShopView(parent, TryGetIcon);
                ShopState state = FakeShopModel.CreateState();
                state.Offers[0].Locked = true;
                view.Render(ShopViewDataBuilder.Build(state, 0, false, ""));
                VisualElement card = parent.Query<VisualElement>(className: "shop-offer-card").ToList()[0];
                Button lockButton = card.Q<Button>(className: "shop-lock");
                Assert.That(card.ClassListContains("shop-offer-locked"), Is.True);
                VisualElement lockStatus = card.Q<VisualElement>(className: "shop-lock-status");
                Assert.That(lockStatus, Is.Not.Null);
                Assert.That(lockStatus.parent.tooltip, Does.Contain("останется при обновлении"));
                Assert.That(lockButton.Q<Label>(className: "shop-command-label").text, Is.EqualTo("Открепить"));
                int lockedOfferSlot = -1;
                view.OnLockOfferClicked += offerSlot => lockedOfferSlot = offerSlot;
                SendPointerEnter(lockButton);
                view.SubmitSelection();
                Assert.That(lockedOfferSlot, Is.Zero);
            }
            finally
            {
                window.Close();
            }
        }

        private static void SendPointerEnter(VisualElement target)
        {
            using PointerEnterEvent pointerEnter = PointerEnterEvent.GetPooled();
            pointerEnter.target = target;
            target.SendEvent(pointerEnter);
        }

        private static void VerifyStatInformation(VisualElement parent)
        {
            using var view = new ShopView(parent, TryGetIcon);
            ShopViewData data = ShopViewDataBuilder.Build(FakeShopModel.CreateState(), 0, false, "");
            data.IsShowingStats = true;
            view.Render(data);
            VisualElement statRow = parent.Query<VisualElement>(className: "shop-stat-row").ToList()
                .Single(row => row.Q<Label>(className: "shop-stat-name").text == "Регенерация");
            Label information = parent.Q<Label>(className: "shop-tooltip");
            using (PointerEnterEvent pointerEnter = PointerEnterEvent.GetPooled())
            {
                pointerEnter.target = statRow;
                statRow.SendEvent(pointerEnter);
            }

            Assert.That(information.ClassListContains("hidden"), Is.False);
            Assert.That(information.text, Does.Contain("HP"));
            using (PointerLeaveEvent pointerLeave = PointerLeaveEvent.GetPooled())
            {
                pointerLeave.target = statRow;
                statRow.SendEvent(pointerLeave);
            }

            Assert.That(information.ClassListContains("hidden"), Is.True);
        }

        private static bool TryGetIcon(int catalogId, out Sprite sprite)
        {
            sprite = null;
            return false;
        }
    }
}
