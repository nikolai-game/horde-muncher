using System.Linq;
using HordeMuncher.UI.Contracts;
using HordeMuncher.UI.Shop.Presentation;
using NUnit.Framework;

namespace HordeMuncher.Tests.Shop
{
    public sealed class ShopPresenterTests
    {
        private FakeShopModel _model;
        private FakeShopView _view;
        private ShopPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _model = new FakeShopModel();
            _view = new FakeShopView();
            _presenter = new ShopPresenter(_model, _model, _view);
            _presenter.Refresh(0);
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
        }

        [Test]
        public void DisplaysAuthoritativeDataWithoutRebuildingUnchangedScreens()
        {
            Assert.That(_view.ViewData.IsVisible, Is.True);
            Assert.That(_view.ViewData.MoneyText, Is.EqualTo("● 100"));
            Assert.That(_view.ViewData.Offers[0].Buttons[0].Enabled, Is.True);
            int renders = _view.RenderCount;
            _presenter.Refresh(1);
            Assert.That(_view.RenderCount, Is.EqualTo(renders));
            _model.State.Offers[0] = new ShopOfferState
            {
                CatalogId = 0,
                Price = 120
            };
            _presenter.Refresh(1.1);
            Assert.That(_view.ViewData.Offers[0].Buttons[0].Enabled, Is.False);
        }

        [Test]
        public void SendsVersionedCommandOnceAndDoesNotChangeMoneyOptimistically()
        {
            _view.ClickBuyOffer();
            _view.ClickBuyOffer();
            Assert.That(_model.Sent.Count, Is.EqualTo(1));
            Assert.That(_model.Sent[0], Is.EqualTo((ShopCommandKind.Buy, 0, 5u)));
            Assert.That(_presenter.IsPending, Is.True);
            Assert.That(_view.ViewData.MoneyText, Is.EqualTo("● 100"));
            Assert.That(_view.ViewData.Offers[0].Buttons[0].Enabled, Is.False);
            Assert.That(_view.ViewData.Reroll.Enabled, Is.False);
        }

        [Test]
        public void TimeoutShowsDelayWithoutAutomaticallyRetryingOrDeclaringFailure()
        {
            _view.ClickBuyOffer();
            _presenter.Refresh(3);
            Assert.That(_presenter.IsPending, Is.True);
            Assert.That(_view.ViewData.FeedbackText, Does.Contain("задерживается"));
            _view.ClickRerollShop();
            Assert.That(_model.Sent.Count, Is.EqualTo(1));
            _model.State.Revision = 6;
            _model.State.Resources = 88;
            _model.State.Offers[0] = new ShopOfferState
            {
                CatalogId = 0,
                Price = 12,
                Sold = true
            };
            _presenter.Refresh(4);
            Assert.That(_presenter.IsPending, Is.False);
            Assert.That(_view.ViewData.MoneyText, Is.EqualTo("● 88"));
            Assert.That(_view.ViewData.Offers.Length, Is.EqualTo(2));
            Assert.That(_view.ViewData.Offers[0].IsSold, Is.True);
            Assert.That(_view.ViewData.Offers[1].Buttons[0].Slot, Is.EqualTo(1));
        }

        [Test]
        public void ServerRejectionReleasesPendingStateAndDisplaysTheReason()
        {
            _view.ClickBuyOffer();
            _model.State.Revision = 6;
            _model.State.Feedback = ShopFeedback.NotEnoughMoney;
            _presenter.Refresh(1);
            Assert.That(_presenter.IsPending, Is.False);
            Assert.That(_view.ViewData.FeedbackText, Is.EqualTo("Недостаточно ресурсов"));
            Assert.That(_view.ViewData.MoneyText, Is.EqualTo("● 100"));
        }

        [Test]
        public void PendingUpgradesBlockPurchasesUntilAChoiceIsMade()
        {
            _model.State.PendingUpgrades = 1;
            _presenter.Refresh(1);
            Assert.That(_view.ViewData.IsLeveling, Is.True);
            Assert.That(_view.ViewData.Offers[0].Buttons[0].Command, Is.EqualTo(ShopCommandKind.Upgrade));
            _view.ClickBuyOffer();
            _view.ClickReadyButton();
            Assert.That(_model.Sent, Is.Empty);
            _view.ClickUpgradeChoice();
            Assert.That(_model.Sent.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReadyPlayerCanCancelReadinessButCannotBuyOrSellLastWeapon()
        {
            _view.ClickSellWeapon();
            Assert.That(_model.Sent, Is.Empty);
            _model.State.Ready = true;
            _presenter.Refresh(1);
            _view.ClickBuyOffer();
            Assert.That(_model.Sent, Is.Empty);
            _view.ClickReadyButton();
            Assert.That(_model.Sent.Count, Is.EqualTo(1));
        }

        [Test]
        public void TransportRefusalDoesNotCreateAnUnresolvablePendingCommand()
        {
            _model.CanSend = false;
            _view.ClickBuyOffer();
            Assert.That(_presenter.IsPending, Is.False);
            Assert.That(_model.Sent, Is.Empty);
            Assert.That(_view.ViewData.FeedbackText, Does.Contain("соединение"));
            _model.State.Revision++;
            _presenter.Refresh(1);
            Assert.That(_view.ViewData.FeedbackText, Is.Empty);
        }

        [Test]
        public void DisconnectAndPhaseChangeHideTheShopAndResetPendingState()
        {
            _view.ClickBuyOffer();
            _model.Available = false;
            _presenter.Refresh(1);
            Assert.That(_presenter.IsVisible, Is.False);
            Assert.That(_presenter.IsPending, Is.False);
            Assert.That(_view.ViewData.IsVisible, Is.False);
            _model.Available = true;
            _model.State.IsOpen = false;
            _presenter.Refresh(2);
            _view.ClickBuyOffer();
            Assert.That(_model.Sent.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReturningToSinglePlayerShowsAllItemsAndResetsTheStatsPage()
        {
            _model.State.Items = Enumerable.Repeat(new ShopOwnedItem { CatalogId = 1, Count = 1 }, 13).ToArray();
            _presenter.SetPlayerCount(2);
            _presenter.Refresh(1);
            _view.ClickNextItemsPage();
            _presenter.ToggleStats();
            Assert.That(_view.ViewData.IsShowingStats, Is.True);

            _presenter.SetPlayerCount(1);
            _presenter.Refresh(2);
            _presenter.ToggleStats();

            Assert.That(_view.ViewData.UsesPages, Is.False);
            Assert.That(_view.ViewData.IsShowingStats, Is.False);
            Assert.That(_view.ViewData.Items.Length, Is.EqualTo(13));
            Assert.That(_view.ViewData.ItemPages, Is.EqualTo(1));
            Assert.That(_model.Sent, Is.Empty);
        }

        [Test]
        public void InventoryPagingIsLocalAndClamped()
        {
            _presenter.SetPlayerCount(2);
            _model.State.Items = new ShopOwnedItem[13];
            for (int ownedItemIndex = 0; ownedItemIndex < 13; ownedItemIndex++)
            {
                _model.State.Items[ownedItemIndex] = new ShopOwnedItem
                {
                    CatalogId = 1,
                    Count = 1
                };
            }

            _presenter.Refresh(1);
            Assert.That(_view.ViewData.Items.Length, Is.EqualTo(12));
            _view.ClickNextItemsPage();
            _view.ClickNextItemsPage();
            Assert.That(_view.ViewData.ItemPage, Is.EqualTo(1));
            Assert.That(_view.ViewData.Items.Length, Is.EqualTo(1));
            Assert.That(_model.Sent, Is.Empty);
        }

        [Test]
        public void StatsToggleIsLocalToEachPlayerAndResetsWhenTheShopCloses()
        {
            _presenter.SetPlayerCount(2);
            _presenter.Refresh(0);
            var otherView = new FakeShopView();
            using var otherPresenter = new ShopPresenter(_model, _model, otherView);
            otherPresenter.SetPlayerCount(2);
            otherPresenter.Refresh(0);
            Assert.That(_view.ViewData.IsShowingStats, Is.False);
            _view.ClickToggleStats();
            Assert.That(_view.ViewData.IsShowingStats, Is.True);
            Assert.That(otherView.ViewData.IsShowingStats, Is.False);
            Assert.That(_model.Sent, Is.Empty);
            _presenter.Refresh(1);
            Assert.That(_view.ViewData.IsShowingStats, Is.True);
            _presenter.ToggleStats();
            Assert.That(_view.ViewData.IsShowingStats, Is.False);
            _presenter.ToggleStats();
            _model.State.IsOpen = false;
            _presenter.Refresh(2);
            _model.State.IsOpen = true;
            _presenter.Refresh(3);
            Assert.That(_view.ViewData.IsShowingStats, Is.False);
        }

        [Test]
        public void ExitRemainsAvailableWhilePendingAndDisposeUnsubscribesFromView()
        {
            int exits = 0;
            _presenter.OnExitToMenuRequested += () => exits++;
            _view.ClickBuyOffer();
            _view.ClickExitToMenu();
            Assert.That(exits, Is.EqualTo(1));
            _presenter.Dispose();
            Assert.That(_view.ClickSubscriberCount, Is.Zero);
            _view.ClickExitToMenu();
            _view.ClickRerollShop();
            _presenter.Refresh(3);
            Assert.That(exits, Is.EqualTo(1));
            Assert.That(_model.Sent.Count, Is.EqualTo(1));
        }

        [TestCase(ShopCommandKind.Buy, 1)]
        [TestCase(ShopCommandKind.Lock, 1)]
        [TestCase(ShopCommandKind.Sell, 1)]
        [TestCase(ShopCommandKind.Merge, 1)]
        [TestCase(ShopCommandKind.Upgrade, 0)]
        [TestCase(ShopCommandKind.Reroll, 0)]
        [TestCase(ShopCommandKind.Ready, 0)]
        public void NamedButtonClicksCreateTheCorrectShopCommand(ShopCommandKind command, int expectedSlot)
        {
            if (command == ShopCommandKind.Sell || command == ShopCommandKind.Merge)
            {
                _model.State.Weapons = new[]
                {
                    0,
                    0
                };
            }

            if (command == ShopCommandKind.Upgrade)
            {
                _model.State.PendingUpgrades = 1;
            }

            _model.State.Catalog[0].UpgradeCatalogId = 1;
            _presenter.Refresh(1);
            switch (command)
            {
                case ShopCommandKind.Buy:
                    _view.ClickBuyOffer(expectedSlot);
                    break;
                case ShopCommandKind.Lock:
                    _view.ClickLockOffer(expectedSlot);
                    break;
                case ShopCommandKind.Sell:
                    _view.ClickSellWeapon(expectedSlot);
                    break;
                case ShopCommandKind.Merge:
                    _view.ClickMergeWeapon(expectedSlot);
                    break;
                case ShopCommandKind.Upgrade:
                    _view.ClickUpgradeChoice(expectedSlot);
                    break;
                case ShopCommandKind.Reroll:
                    _view.ClickRerollShop();
                    break;
                case ShopCommandKind.Ready:
                    _view.ClickReadyButton();
                    break;
            }

            Assert.That(_model.Sent.Count, Is.EqualTo(1));
            Assert.That(_model.Sent[0], Is.EqualTo((command, expectedSlot, 5u)));
            Assert.That(_view.ClickSubscriberCount, Is.EqualTo(14));
            _presenter.Dispose();
            Assert.That(_view.ClickSubscriberCount, Is.Zero);
        }

        [Test]
        public void PresentersDoNotDependOnUnityOrNetworkingAssemblies()
        {
            string[] references = typeof(ShopPresenter).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
            Assert.That(references.Any(name => name.StartsWith("Unity") || name == "HordeMuncher.Runtime"), Is.False);
            Assert.That(typeof(IShopView).Assembly.GetReferencedAssemblies().Any(assembly => assembly.Name.StartsWith("Unity")), Is.False);
        }
    }
}
