using HordeMuncher.UI.Contracts;
using HordeMuncher.UI.Shop.Presentation;
using HordeMuncher.UI.Shop.View;
using NUnit.Framework;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

namespace HordeMuncher.Tests.Shop
{
    public sealed class PostBattleRewardTests
    {
        [TestCase(ShopAction.TakeItemReward, 120, 100, 3)]
        [TestCase(ShopAction.RecycleItemReward, 100, 104, 2)]
        public void ItemRewardIsResolvedOnceAndBlocksTrading(ShopAction action, int maximumHealth, int resources, int itemCount)
        {
            using var arena = new ShopBridgeTestArena(isServer: true);
            EntityManager manager = arena.World.EntityManager;
            manager.AddBuffer<PendingItemReward>(arena.Player).Add(new PendingItemReward { CatalogId = 1 });
            EconomyConfig config = manager.GetComponentData<EconomyConfig>(arena.Config);
            DynamicBuffer<CatalogEntry> catalog = manager.GetBuffer<CatalogEntry>(arena.Config);
            Assert.That(EconomyRules.ApplyShopRequest(manager, arena.Player,
                new ShopRequest { Action = ShopAction.Buy, Revision = 5 }, config, catalog), Is.EqualTo(ShopResult.ResolveUpgrades));
            Assert.That(EconomyRules.ApplyShopRequest(manager, arena.Player,
                new ShopRequest { Action = ShopAction.Ready, Revision = 6 }, config, catalog), Is.EqualTo(ShopResult.ResolveUpgrades));
            var request = new ShopRequest { Action = action, Revision = 7 };
            Assert.That(EconomyRules.ApplyShopRequest(manager, arena.Player, request, config, catalog), Is.EqualTo(ShopResult.Success));
            Assert.That(EconomyRules.ApplyShopRequest(manager, arena.Player, request, config, catalog), Is.EqualTo(ShopResult.Invalid));
            Assert.That(manager.GetBuffer<PendingItemReward>(arena.Player).Length, Is.Zero);
            Assert.That(manager.GetComponentData<CombatHealth>(arena.Player).Maximum, Is.EqualTo(maximumHealth));
            Assert.That(manager.GetComponentData<CombatWallet>(arena.Player).Resources, Is.EqualTo(resources));
            Assert.That(manager.GetBuffer<OwnedItem>(arena.Player)[0].Count, Is.EqualTo(itemCount));
        }

        [Test]
        public void StoreWaitsForBothPlayersToFinishRewards()
        {
            using var arena = new ShopBridgeTestArena(isServer: true);
            EntityManager manager = arena.World.EntityManager;
            Entity teammate = arena.CreatePlayer(2, false);
            manager.AddComponentData(arena.Player, LocalTransform.Identity);
            manager.AddComponentData(teammate, LocalTransform.Identity);
            manager.AddBuffer<PendingItemReward>(teammate).Add(new PendingItemReward { CatalogId = 1 });
            Entity run = manager.CreateEntity(typeof(WaveRuntimeSettings), typeof(WaveProgress));
            manager.SetComponentData(run, new WaveProgress { Phase = WavePhase.Rewards });
            RunLifecycleSystem lifecycle = arena.World.CreateSystemManaged<RunLifecycleSystem>();
            lifecycle.Update();
            Assert.That(manager.GetComponentData<WaveProgress>(run).Phase, Is.EqualTo(WavePhase.Rewards));
            manager.GetBuffer<PendingItemReward>(teammate).Clear();
            PlayerProgress progress = manager.GetComponentData<PlayerProgress>(arena.Player);
            progress.PendingUpgrades = 1;
            manager.SetComponentData(arena.Player, progress);
            lifecycle.Update();
            Assert.That(manager.GetComponentData<WaveProgress>(run).Phase, Is.EqualTo(WavePhase.Rewards));
            progress.PendingUpgrades = 0;
            manager.SetComponentData(arena.Player, progress);
            lifecycle.Update();
            Assert.That(manager.GetComponentData<WaveProgress>(run).Phase, Is.EqualTo(WavePhase.Shop));
        }

        [Test]
        public void RewardsAndStoreRenderSeparateScreens()
        {
            ShopState state = FakeShopModel.CreateState();
            state.IsRewardScreen = true;
            state.PendingUpgrades = 1;
            state.PendingItemRewards = new[] { 1 };
            var parent = new VisualElement();
            using var view = new ShopView(parent, TryGetIcon);
            view.Render(ShopViewDataBuilder.Build(state, 0, false, "", true, true));
            Assert.That(parent.Q(className: "reward-content"), Is.Not.Null);
            Assert.That(parent.Q(className: "shop-frame"), Is.Null);
            Assert.That(parent.Q(className: "shop-inventory"), Is.Null);
            Assert.That(parent.Q(className: "shop-ready"), Is.Null);
            Assert.That(ShopActionAvailability.CanExecute(state, ShopCommandKind.Ready, 0), Is.False);
            state.PendingUpgrades = 0;
            ShopViewData rewards = ShopViewDataBuilder.Build(state, 0, false, "");
            Assert.That(rewards.Offers[0].Buttons[0].Command, Is.EqualTo(ShopCommandKind.TakeItemReward));
            view.Render(rewards);
            state.PendingItemRewards = System.Array.Empty<int>();
            view.Render(ShopViewDataBuilder.Build(state, 0, false, ""));
            Assert.That(parent.Q(className: "shop-offer-card"), Is.Null);
            state.IsRewardScreen = false;
            view.Render(ShopViewDataBuilder.Build(state, 0, false, ""));
            Assert.That(parent.Q(className: "reward-content"), Is.Null);
            Assert.That(parent.Q(className: "shop-frame"), Is.Not.Null);
            Assert.That(parent.Q(className: "shop-ready"), Is.Not.Null);
        }

        private static bool TryGetIcon(int catalogId, out Sprite sprite)
        {
            sprite = null;
            return false;
        }
    }
}
