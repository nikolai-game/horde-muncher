using HordeMuncher.UI.Contracts;
using HordeMuncher.UI.Shop.Presentation;
using NUnit.Framework;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher.Tests.Shop
{
    public sealed class UpgradeRerollTests
    {
        [TestCase(true, 0, ShopResult.Success)]
        [TestCase(false, 0, ShopResult.NotEnoughMoney)]
        [TestCase(false, 20, ShopResult.Success)]
        public void ShopRerollIsFreeOnlyWhenAllOffersAreSold(bool allSold, int resources, ShopResult expectedResult)
        {
            using var arena = new ShopBridgeTestArena();
            EntityManager manager = arena.World.EntityManager;
            DynamicBuffer<ShopOffer> offers = manager.GetBuffer<ShopOffer>(arena.Player);
            for (int offerSlot = 0; offerSlot < offers.Length; offerSlot++)
            {
                ShopOffer offer = offers[offerSlot];
                offer.Sold = allSold || offerSlot == 0;
                offers[offerSlot] = offer;
            }

            PlayerProgress progress = manager.GetComponentData<PlayerProgress>(arena.Player);
            progress.Rerolls = 3;
            manager.SetComponentData(arena.Player, progress);
            manager.SetComponentData(arena.Player, new CombatWallet { Resources = resources });
            Assert.That(arena.Gateway.TryRead(out ShopState state), Is.True);
            Assert.That(state.RerollPrice, Is.EqualTo(allSold ? 0 : 5));
            Assert.That(ShopActionAvailability.CanExecute(state, ShopCommandKind.Reroll, 0),
                Is.EqualTo(expectedResult == ShopResult.Success));
            ShopResult result = EconomyRules.ApplyShopRequest(manager, arena.Player,
                new ShopRequest { Action = ShopAction.Reroll, Revision = progress.Revision },
                manager.GetComponentData<EconomyConfig>(arena.Config), manager.GetBuffer<CatalogEntry>(arena.Config));
            Assert.That(result, Is.EqualTo(expectedResult));
            int charge = !allSold && expectedResult == ShopResult.Success ? 5 : 0;
            Assert.That(manager.GetComponentData<CombatWallet>(arena.Player).Resources, Is.EqualTo(resources - charge));
            if (expectedResult == ShopResult.Success)
            {
                offers = manager.GetBuffer<ShopOffer>(arena.Player);
                Assert.That(offers.Length, Is.EqualTo(4));
                foreach (ShopOffer offer in offers)
                {
                    Assert.That(offer.Sold, Is.False);
                }
            }
        }

        [Test]
        public void PaidRerollsReplaceChoicesAndGrowInPriceWithoutConsumingLevelsOrShopOffers()
        {
            using var arena = new ShopBridgeTestArena(isServer: true);
            EntityManager manager = arena.World.EntityManager;
            var progress = new PlayerProgress { PendingUpgrades = 2, RandomState = 17, PriceMultiplier = 1.25f };
            manager.SetComponentData(arena.Player, progress);
            EconomyConfig config = manager.GetComponentData<EconomyConfig>(arena.Config);
            DynamicBuffer<CatalogEntry> catalog = manager.GetBuffer<CatalogEntry>(arena.Config);
            var request = new ShopRequest { Action = ShopAction.RerollUpgrades };

            Assert.That(EconomyRules.ApplyShopRequest(manager, arena.Player, request, config, catalog), Is.EqualTo(ShopResult.Success));
            progress = manager.GetComponentData<PlayerProgress>(arena.Player);
            Assert.That(manager.GetComponentData<CombatWallet>(arena.Player).Resources, Is.EqualTo(97));
            Assert.That(progress.PendingUpgrades, Is.EqualTo(2));
            Assert.That(progress.UpgradeRerolls, Is.EqualTo(1));
            Assert.That(progress.Rerolls, Is.Zero);
            Assert.That(manager.GetBuffer<UpgradeChoice>(arena.Player).Length, Is.EqualTo(4));
            Assert.That(manager.GetBuffer<ShopOffer>(arena.Player).Length, Is.EqualTo(2));
            Assert.That(EconomyRules.CalculateUpgradeRerollPrice(config, progress), Is.EqualTo(4));
            uint randomState = progress.RandomState;
            Assert.That(EconomyRules.ApplyShopRequest(manager, arena.Player, request, config, catalog), Is.EqualTo(ShopResult.Invalid));
            Assert.That(manager.GetComponentData<CombatWallet>(arena.Player).Resources, Is.EqualTo(97));

            request.Revision = progress.Revision;
            Assert.That(EconomyRules.ApplyShopRequest(manager, arena.Player, request, config, catalog), Is.EqualTo(ShopResult.Success));
            progress = manager.GetComponentData<PlayerProgress>(arena.Player);
            Assert.That(manager.GetComponentData<CombatWallet>(arena.Player).Resources, Is.EqualTo(93));
            Assert.That(progress.RandomState, Is.Not.EqualTo(randomState));
            Assert.That(progress.UpgradeRerolls, Is.EqualTo(2));
            request = new ShopRequest { Action = ShopAction.Upgrade, Slot = 0, Revision = progress.Revision };
            Assert.That(EconomyRules.ApplyShopRequest(manager, arena.Player, request, config, catalog), Is.EqualTo(ShopResult.Success));
            Assert.That(manager.GetComponentData<PlayerProgress>(arena.Player).UpgradeRerolls, Is.EqualTo(2));
        }

        [TestCase(1, false, 1, ShopResult.NotEnoughMoney)]
        [TestCase(0, false, 100, ShopResult.Invalid)]
        [TestCase(1, true, 100, ShopResult.AlreadyReady)]
        public void RejectedRerollsDoNotChargeOrChangeChoices(int pendingUpgrades, bool ready, int resources, ShopResult expectedResult)
        {
            using var arena = new ShopBridgeTestArena(isServer: true);
            EntityManager manager = arena.World.EntityManager;
            manager.SetComponentData(arena.Player, new PlayerProgress
            {
                PendingUpgrades = pendingUpgrades,
                Ready = ready,
                RandomState = 17
            });
            manager.SetComponentData(arena.Player, new CombatWallet { Resources = resources });
            ShopResult result = EconomyRules.ApplyShopRequest(manager, arena.Player,
                new ShopRequest { Action = ShopAction.RerollUpgrades },
                manager.GetComponentData<EconomyConfig>(arena.Config), manager.GetBuffer<CatalogEntry>(arena.Config));
            Assert.That(result, Is.EqualTo(expectedResult));
            Assert.That(manager.GetComponentData<CombatWallet>(arena.Player).Resources, Is.EqualTo(resources));
            PlayerProgress progress = manager.GetComponentData<PlayerProgress>(arena.Player);
            Assert.That(progress.UpgradeRerolls, Is.Zero);
            Assert.That(progress.RandomState, Is.EqualTo(17));
            Assert.That(manager.GetBuffer<UpgradeChoice>(arena.Player).Length, Is.EqualTo(1));
        }

        [TestCase(2, 4)]
        [TestCase(4, 6)]
        [TestCase(8, 10)]
        [TestCase(24, 20)]
        public void LaterWavesIncreasePurchasingPowerAndSavingAllowsExtraPurchases(int incomeMultiplier, int minimumPurchasingPower)
        {
            using var arena = new ShopBridgeTestArena(isServer: true);
            EntityManager manager = arena.World.EntityManager;
            DynamicBuffer<WavePlan> waves = manager.AddBuffer<WavePlan>(arena.Config);
            waves.Add(new WavePlan { Duration = 20, SpawnRatePointCount = 2 });
            waves.Add(new WavePlan { Duration = 20 * incomeMultiplier, SpawnRatePointOffset = 2, SpawnRatePointCount = 2 });
            DynamicBuffer<WaveSpawnRateCurvePoint> rates = manager.AddBuffer<WaveSpawnRateCurvePoint>(arena.Config);
            for (int curvePointIndex = 0; curvePointIndex < 4; curvePointIndex++)
            {
                rates.Add(new WaveSpawnRateCurvePoint { Rate = 1 });
            }

            EconomySettings settings = UnityEditor.AssetDatabase.LoadAssetAtPath<EconomySettings>("Assets/Project/Configs/Economy.asset");
            waves = manager.GetBuffer<WavePlan>(arena.Config);
            float priceMultiplier = EconomyRules.CalculateWavePriceMultiplier(waves, rates, 1, settings.EnemyPriceGrowth);
            int itemPrice = EconomyRules.CalculatePrice(20, priceMultiplier);
            int waveIncome = 60 * incomeMultiplier;
            float purchasingPower = (float)waveIncome / itemPrice;
            Assert.That(priceMultiplier, Is.GreaterThan(1).And.LessThan(incomeMultiplier));
            Assert.That(purchasingPower, Is.GreaterThan(minimumPurchasingPower),
                "Extra income must allow more purchases instead of approaching a fixed purchasing limit.");
            Assert.That((waveIncome + waveIncome / 2) / itemPrice, Is.GreaterThan(waveIncome / itemPrice),
                "Money saved from an earlier wave should allow additional purchases.");
        }

        [Test]
        public void EnemyGrowthRaisesOfferAndRerollPricesIncludingLockedOffers()
        {
            using var arena = new ShopBridgeTestArena(isServer: true);
            EntityManager manager = arena.World.EntityManager;
            DynamicBuffer<WavePlan> waves = manager.AddBuffer<WavePlan>(arena.Config);
            waves.Add(new WavePlan { Duration = 10, SpawnRatePointCount = 2 });
            waves.Add(new WavePlan { Duration = 20, SpawnRatePointOffset = 2, SpawnRatePointCount = 2 });
            DynamicBuffer<WaveSpawnRateCurvePoint> rates = manager.AddBuffer<WaveSpawnRateCurvePoint>(arena.Config);
            for (int curvePointIndex = 0; curvePointIndex < 4; curvePointIndex++)
            {
                rates.Add(new WaveSpawnRateCurvePoint { Rate = 2 });
            }

            waves = manager.GetBuffer<WavePlan>(arena.Config);
            float multiplier = EconomyRules.CalculateWavePriceMultiplier(waves, rates, 1, 0.25f);
            Assert.That(EconomyRules.CalculateWavePriceMultiplier(waves, rates, 0, 0.25f), Is.EqualTo(1));
            Assert.That(multiplier, Is.EqualTo(1.25f));
            DynamicBuffer<ShopOffer> offers = manager.GetBuffer<ShopOffer>(arena.Player);
            offers[0] = new ShopOffer { CatalogId = 0, Price = 12, Locked = true };
            var progress = new PlayerProgress { RandomState = 17, PriceMultiplier = multiplier, Rerolls = 2 };
            EconomyRules.RefreshShopOffers(manager, arena.Player, manager.GetBuffer<CatalogEntry>(arena.Config), ref progress);
            Assert.That(offers[0].CatalogId, Is.Zero);
            Assert.That(offers[0].Locked, Is.True);
            foreach (ShopOffer offer in offers)
            {
                int basePrice = manager.GetBuffer<CatalogEntry>(arena.Config)[offer.CatalogId].Price;
                Assert.That(offer.Price, Is.EqualTo(EconomyRules.CalculatePrice(basePrice, multiplier)));
            }

            Assert.That(EconomyRules.CalculateShopRerollPrice(manager.GetComponentData<EconomyConfig>(arena.Config), progress), Is.EqualTo(5));
        }

        [Test]
        public void LevelingButtonAndShortcutSendUpgradeRerollsAndBlockRepeatedPendingClicks()
        {
            var model = new FakeShopModel();
            var view = new FakeShopView();
            model.State.PendingUpgrades = 1;
            using var presenter = new ShopPresenter(model, model, view);
            presenter.Refresh(0);
            Assert.That(view.ViewData.Reroll.Command, Is.EqualTo(ShopCommandKind.RerollUpgrades));
            Assert.That(view.ViewData.Reroll.Text, Does.Contain("2"));
            view.ClickRerollUpgrades();
            presenter.RerollShop();
            Assert.That(model.Sent.Count, Is.EqualTo(1));
            Assert.That(model.Sent[0].Command, Is.EqualTo(ShopCommandKind.RerollUpgrades));
            Assert.That(view.ViewData.Reroll.Enabled, Is.False);
            model.State.Revision++;
            model.State.Resources = 1;
            presenter.Refresh(1);
            Assert.That(view.ViewData.Reroll.Enabled, Is.False);
        }

        [Test]
        public void BridgeShowsTheServerPriceAndSendsTheUpgradeRerollRpc()
        {
            using var arena = new ShopBridgeTestArena();
            EntityManager manager = arena.World.EntityManager;
            PlayerProgress progress = manager.GetComponentData<PlayerProgress>(arena.Player);
            progress.PendingUpgrades = 1;
            progress.UpgradeRerolls = 2;
            progress.PriceMultiplier = 1.25f;
            manager.SetComponentData(arena.Player, progress);
            Assert.That(arena.Gateway.TryRead(out ShopState state), Is.True);
            Assert.That(state.UpgradeRerollPrice, Is.EqualTo(5));
            Assert.That(arena.Gateway.TrySend(ShopCommandKind.RerollUpgrades, 0, state.Revision), Is.True);
            using EntityQuery requests = manager.CreateEntityQuery(typeof(ShopRequest), typeof(SendRpcCommandRequest));
            Assert.That(requests.GetSingleton<ShopRequest>().Action, Is.EqualTo(ShopAction.RerollUpgrades));
            Assert.That(manager.GetComponentData<CombatWallet>(arena.Player).Resources, Is.EqualTo(100));
        }
    }
}
