using System.Collections;
using System.Linq;
using HordeMuncher.UI.Contracts;
using HordeMuncher.UI.Shop.Presentation;
using HordeMuncher.UI.Shop.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;

namespace HordeMuncher.Tests.Shop
{
    public sealed class ShopShortcutTests
    {
        [Test]
        public void LockShortcutTargetsSelectedOfferAndPreservesTheBuySelection()
        {
            var window = ScriptableObject.CreateInstance<UnityEditor.EditorWindow>();
            try
            {
                window.Show();
                VisualElement parent = window.rootVisualElement;
                using var view = new ShopView(parent, TryGetIcon);
                ShopViewData data = ShopViewDataBuilder.Build(FakeShopModel.CreateState(), 0, false, "");
                data.Reroll.Enabled = false;
                int lockedSlot = -1;
                int boughtSlot = -1;
                view.OnLockOfferClicked += offerSlot => lockedSlot = offerSlot;
                view.OnBuyOfferClicked += offerSlot => boughtSlot = offerSlot;
                view.Render(data);

                Button buyButton = parent.Q<Button>(className: "shop-buy");
                using PointerEnterEvent pointerEnter = PointerEnterEvent.GetPooled();
                pointerEnter.target = buyButton;
                buyButton.SendEvent(pointerEnter);

                view.LockSelectedOffer();
                view.SubmitSelection();

                Assert.That(lockedSlot, Is.EqualTo(data.Offers[0].Buttons[0].Slot));
                Assert.That(boughtSlot, Is.EqualTo(lockedSlot));
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void RerollShortcutKeepsPendingAndVisibilityGuards()
        {
            var model = new FakeShopModel();
            var view = new FakeShopView();
            using var presenter = new ShopPresenter(model, model, view);
            presenter.RerollShop();
            Assert.That(model.Sent, Is.Empty);

            presenter.Refresh(0);
            presenter.RerollShop();
            presenter.RerollShop();
            Assert.That(model.Sent.Count, Is.EqualTo(1));
            Assert.That(model.Sent[0].Command, Is.EqualTo(ShopCommandKind.Reroll));

            model.State.IsOpen = false;
            presenter.Refresh(1);
            presenter.RerollShop();
            Assert.That(model.Sent.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReadyShortcutWaitsForFullHoldAndRequiresReleaseBeforeRepeating()
        {
            var model = new FakeShopModel();
            var view = new FakeShopView();
            using var presenter = new ShopPresenter(model, model, view);
            presenter.Refresh(0);
            presenter.UpdateReadyHold(true, 0);
            presenter.UpdateReadyHold(true, 0.5);
            Assert.That(model.Sent, Is.Empty);
            Assert.That(view.ReadyHoldProgress, Is.EqualTo(0.5f));
            presenter.UpdateReadyHold(true, 1);
            Assert.That(model.Sent.Count, Is.EqualTo(1));
            Assert.That(model.Sent[0].Command, Is.EqualTo(ShopCommandKind.Ready));
            Assert.That(view.ReadyHoldProgress, Is.Zero);
            model.State.Revision++;
            presenter.Refresh(2);
            presenter.UpdateReadyHold(true, 2);
            presenter.UpdateReadyHold(true, 3);
            Assert.That(model.Sent.Count, Is.EqualTo(1));
            presenter.UpdateReadyHold(false, 3);
            presenter.UpdateReadyHold(true, 4);
            presenter.UpdateReadyHold(true, 5);
            Assert.That(model.Sent.Count, Is.EqualTo(2));
        }

        [Test]
        public void ReadyShortcutResetsOnReleaseAndCannotSkipRewards()
        {
            var model = new FakeShopModel();
            var view = new FakeShopView();
            using var presenter = new ShopPresenter(model, model, view);
            presenter.Refresh(0);
            presenter.UpdateReadyHold(true, 0);
            presenter.UpdateReadyHold(true, 0.5);
            presenter.UpdateReadyHold(false, 0.6);
            Assert.That(view.ReadyHoldProgress, Is.Zero);
            presenter.UpdateReadyHold(true, 1);
            presenter.UpdateReadyHold(true, 1.5);
            Assert.That(model.Sent, Is.Empty);
            model.State.PendingUpgrades = 1;
            presenter.Refresh(2);
            presenter.UpdateReadyHold(true, 3);
            Assert.That(view.ReadyHoldProgress, Is.Zero);
            Assert.That(model.Sent, Is.Empty);
        }

        [UnityTest]
        public IEnumerator UnaffordableOffersRemainNavigableAndLockableWithoutAllowingPurchase()
        {
            var window = ScriptableObject.CreateInstance<UnityEditor.EditorWindow>();
            try
            {
                window.position = new Rect(50, 50, 1200, 720);
                window.Show();
                window.rootVisualElement.styleSheets.Add(
                    UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Project/UI/ArenaMenu.uss"));
                using var view = new ShopView(window.rootVisualElement, TryGetIcon);
                ShopState state = FakeShopModel.CreateState();
                state.Resources = 0;
                ShopViewData data = ShopViewDataBuilder.Build(state, 0, false, "");
                int boughtSlot = -1;
                int lockedSlot = -1;
                view.OnBuyOfferClicked += offerSlot => boughtSlot = offerSlot;
                view.OnLockOfferClicked += offerSlot => lockedSlot = offerSlot;
                view.SetControlHints("E", "F", "Shift+R");
                view.Render(data);
                view.SetPlayerLayout(0, 1);
                yield return null;
                yield return null;

                Button[] buyButtons = view.Viewport.Query<Button>(className: "shop-buy").ToList().ToArray();
                Assert.That(buyButtons[0].Q<Label>(className: "shop-key-hint").text, Is.EqualTo("E"));
                Assert.That(view.Viewport.Q<Button>(className: "shop-reroll").Q<Label>(className: "shop-key-hint").text,
                    Is.EqualTo("Shift+R"));
                Button reroll = view.Viewport.Q<Button>(className: "shop-reroll");
                using (PointerEnterEvent initialRerollHover = PointerEnterEvent.GetPooled())
                {
                    initialRerollHover.target = reroll;
                    reroll.SendEvent(initialRerollHover);
                }

                Assert.That(reroll.ClassListContains("shop-selected"), Is.True);
                view.MoveSelection(0, -1);
                Assert.That(view.Viewport.Query<Button>(className: "shop-lock").ToList()[0]
                    .ClassListContains("shop-selected"), Is.True);
                view.MoveSelection(-1, 0);
                Assert.That(buyButtons[0].ClassListContains("shop-selected"), Is.True);
                Assert.That(buyButtons.All(button => button.ClassListContains("shop-action-unavailable")), Is.True);
                view.MoveSelection(0, -1);
                Assert.That(buyButtons[1].ClassListContains("shop-selected"), Is.True);
                view.SubmitSelection();
                Assert.That(boughtSlot, Is.EqualTo(-1));
                view.LockSelectedOffer();
                Assert.That(lockedSlot, Is.EqualTo(1));
                yield return null;
                yield return null;

                VisualElement actions = view.Viewport.Query<VisualElement>(className: "shop-context-visible").ToList().Single();
                VisualElement offers = view.Viewport.Q<VisualElement>(className: "shop-offers");
                Assert.That(actions.worldBound.yMin, Is.GreaterThanOrEqualTo(offers.worldBound.yMax));
                Assert.That(actions.parent, Is.SameAs(view.Viewport.Q<VisualElement>(className: "shop-details")));
                Assert.That(actions.Q<Label>(className: "shop-offer-name").text, Is.EqualTo(data.Offers[1].Name));

                view.Render(ShopViewDataBuilder.Build(state, 0, false, ""));
                Assert.That(view.Viewport.Query<Button>(className: "shop-buy").ToList()[1]
                    .ClassListContains("shop-selected"), Is.True);
                yield return null;
                yield return null;
                Button secondOffer = view.Viewport.Query<Button>(className: "shop-buy").ToList()[1];
                using (PointerEnterEvent restoredOfferHover = PointerEnterEvent.GetPooled())
                {
                    restoredOfferHover.target = secondOffer;
                    secondOffer.SendEvent(restoredOfferHover);
                }

                view.MoveSelection(0, 1);
                Assert.That(view.Viewport.Query<Button>(className: "shop-buy").ToList()[0]
                    .ClassListContains("shop-selected"), Is.True);
                view.MoveSelection(0, 1);
                Assert.That(view.Viewport.Q<Button>(className: "shop-reroll").ClassListContains("shop-selected"), Is.True);
                int rerollCount = 0;
                view.OnRerollShopClicked += () => rerollCount++;
                view.SubmitSelection();
                Assert.That(rerollCount, Is.Zero);
                using PointerEnterEvent pointerEnter = PointerEnterEvent.GetPooled();
                pointerEnter.target = view.Viewport.Query<Button>(className: "shop-buy").ToList()[0];
                view.Viewport.Query<Button>(className: "shop-buy").ToList()[0].SendEvent(pointerEnter);
                Assert.That(view.Viewport.Query<Button>(className: "shop-buy").ToList()[0]
                    .ClassListContains("shop-selected"), Is.True);
                using PointerEnterEvent rerollPointerEnter = PointerEnterEvent.GetPooled();
                Button rerollButton = view.Viewport.Q<Button>(className: "shop-reroll");
                rerollPointerEnter.target = rerollButton;
                rerollButton.SendEvent(rerollPointerEnter);
                Assert.That(rerollButton.ClassListContains("shop-selected"), Is.True);
                view.SubmitSelection();
                Assert.That(rerollCount, Is.Zero);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void PurchasedOffersKeepEmptySlotsAndRemainingCommandsKeepTheirOriginalSlots()
        {
            ShopState state = FakeShopModel.CreateState();
            state.Offers[0].Sold = true;
            ShopViewData data = ShopViewDataBuilder.Build(state, 0, false, "");
            Assert.That(data.Offers.Length, Is.EqualTo(2));
            Assert.That(data.Offers[0].IsSold, Is.True);
            Assert.That(data.Offers[1].Buttons.All(button => button.Slot == 1), Is.True);
            var parent = new VisualElement();
            using var view = new ShopView(parent, TryGetIcon);
            view.Render(data);
            var cards = parent.Query<VisualElement>(className: "shop-offer-card").ToList();
            Assert.That(cards.Count, Is.EqualTo(2));
            Assert.That(cards[0].ClassListContains("shop-sold"), Is.True);
            Assert.That(cards[1].ClassListContains("shop-sold"), Is.False);
            state.Offers[1].Sold = true;
            Assert.That(ShopViewDataBuilder.Build(state, 0, false, "").Offers.All(card => card.IsSold), Is.True);
        }

        [TestCase(0)]
        [TestCase(2)]
        public void MergeShortcutHighlightsExactPairWaitsForServerAndKeepsUpgradedWeaponSelected(int selectedSlot)
        {
            var window = ScriptableObject.CreateInstance<UnityEditor.EditorWindow>();
            try
            {
                window.Show();
                var model = new FakeShopModel();
                ShopProduct source = model.State.Catalog[0];
                source.Rarity = "Common";
                source.UpgradeCatalogId = 2;
                ShopProduct result = source;
                result.Rarity = "Rare";
                result.UpgradeCatalogId = 0;
                result.Damage = 25;
                model.State.Catalog = new[] { source, model.State.Catalog[1], result };
                model.State.Weapons = new[] { 0, 0, 0 };
                using var view = new ShopView(window.rootVisualElement, TryGetIcon);
                using var otherView = new ShopView(window.rootVisualElement, TryGetIcon);
                using var presenter = new ShopPresenter(model, model, view);
                view.SetControlHints("E", "F", "Shift+R", mergeHint: "R");
                presenter.Refresh(0);
                otherView.Render(ShopViewDataBuilder.Build(model.State, 0, false, ""));
                Button[] weapons = view.Viewport.Query<Button>(className: "shop-weapon-tile").ToList().ToArray();
                using (PointerEnterEvent pointerEnter = PointerEnterEvent.GetPooled())
                {
                    pointerEnter.target = weapons[selectedSlot];
                    weapons[selectedSlot].SendEvent(pointerEnter);
                }

                int partnerSlot = selectedSlot == 0 ? 1 : 0;
                Assert.That(weapons.Select(weapon => weapon.ClassListContains("shop-merge-source")),
                    Is.EqualTo(Enumerable.Range(0, 3).Select(slot => slot == selectedSlot || slot == partnerSlot)));
                Assert.That(otherView.Viewport.Query<Button>(className: "shop-merge-source").ToList(), Is.Empty);
                VisualElement actions = view.Viewport.Q<VisualElement>(className: "shop-context-visible");
                Assert.That(actions.Q<Label>(className: "shop-merge-notice").text, Does.Contain("Rare"));
                Assert.That(actions.Q<Button>(className: "shop-merge").Q<Label>(className: "shop-key-hint").text, Is.EqualTo("R"));

                presenter.MergeSelectedWeapon();
                presenter.MergeSelectedWeapon();
                Assert.That(model.Sent.Count, Is.EqualTo(1));
                Assert.That(model.Sent[0].Command, Is.EqualTo(ShopCommandKind.Merge));
                Assert.That(model.Sent[0].Slot, Is.EqualTo(selectedSlot));
                Assert.That(view.Viewport.Query<Button>(className: "shop-weapon-tile").ToList()[selectedSlot]
                    .ClassListContains("shop-selected"), Is.True);

                model.State.Weapons = selectedSlot == 0 ? new[] { 2, 0 } : new[] { 0, 2 };
                model.State.Revision++;
                presenter.Refresh(1);
                int upgradedSlot = selectedSlot == 0 ? 0 : 1;
                Assert.That(view.Viewport.Query<Button>(className: "shop-weapon-tile").ToList()[upgradedSlot]
                    .ClassListContains("shop-selected"), Is.True);
                Assert.That(view.Viewport.Query<Button>(className: "shop-merge-source").ToList(), Is.Empty);
                presenter.MergeSelectedWeapon();
                Assert.That(model.Sent.Count, Is.EqualTo(1));
            }
            finally
            {
                window.Close();
            }
        }

        private static bool TryGetIcon(int catalogId, out Sprite sprite)
        {
            sprite = null;
            return false;
        }
    }
}
