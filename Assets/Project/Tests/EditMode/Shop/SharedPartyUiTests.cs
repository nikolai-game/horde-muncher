using System.Collections.Generic;
using System.Linq;
using HordeMuncher.Client.PlayerStatus;
using HordeMuncher.Client.Shop;
using HordeMuncher.UI.Contracts;
using HordeMuncher.Tests.PlayerStatus;
using HordeMuncher.UI.Hud;
using UnityEditor;
using HordeMuncher.UI.Shop.Presentation;
using HordeMuncher.UI.Shop.View;
using NUnit.Framework;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UIElements;

namespace HordeMuncher.Tests.Shop
{
    public sealed class SharedPartyUiTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void RosterShowsEveryPlayerOnceAndRoutesLocalPairToTheirOwnWorlds(int playerCount)
        {
            using var first = new ShopBridgeTestArena(1);
            using var second = new ShopBridgeTestArena(2);
            for (int playerNumber = playerCount; playerNumber >= 1; playerNumber--)
            {
                if (playerNumber != 1)
                {
                    first.CreatePlayer(playerNumber, false);
                }

                if (playerNumber != 2)
                {
                    second.CreatePlayer(playerNumber, false);
                }
            }

            List<ClientArenaPlayer> players = ClientArenaRoster.Read(new[] { first.World, second.World });
            Assert.That(players.Select(player => player.PlayerNumber), Is.EqualTo(Enumerable.Range(1, playerCount)));
            Assert.That(players[0].World, Is.SameAs(first.World));
            Assert.That(players[0].LocalIndex, Is.Zero);
            Assert.That(players[1].World, Is.SameAs(second.World));
            Assert.That(players[1].LocalIndex, Is.EqualTo(1));
            foreach (ClientArenaPlayer remote in players.Skip(2))
            {
                Assert.That(remote.LocalIndex, Is.EqualTo(-1));
            }
        }

        [Test]
        public void RosterReflectsJoinDespawnAndConnectionLoss()
        {
            using var arena = new ShopBridgeTestArena();
            var worlds = new[] { arena.World };
            Assert.That(ClientArenaRoster.Read(worlds).Count, Is.EqualTo(1));
            Entity remote = arena.CreatePlayer(4, false);
            Assert.That(ClientArenaRoster.Read(worlds).Select(player => player.PlayerNumber), Is.EqualTo(new[] { 1, 4 }));
            arena.World.EntityManager.DestroyEntity(remote);
            Assert.That(ClientArenaRoster.Read(worlds).Count, Is.EqualTo(1));
            arena.World.EntityManager.AddComponent<NetworkStreamRequestDisconnect>(arena.Connection);
            Assert.That(ClientArenaRoster.Read(worlds), Is.Empty);
        }

        [TestCase(ShopCommandKind.Buy)]
        [TestCase(ShopCommandKind.Lock)]
        [TestCase(ShopCommandKind.Reroll)]
        [TestCase(ShopCommandKind.Ready)]
        [TestCase(ShopCommandKind.Sell)]
        [TestCase(ShopCommandKind.Upgrade)]
        public void RemoteOffersAndBuildAreVisibleButNeverProduceCommands(ShopCommandKind command)
        {
            using var arena = new ShopBridgeTestArena();
            Entity remote = arena.CreatePlayer(3, false);
            using var gateway = new ClientShopGateway(arena.World, 3);
            Assert.That(gateway.TryRead(out ShopState state), Is.True);
            Assert.That(state.PlayerNumber, Is.EqualTo(3));
            Assert.That(state.CanControl, Is.False);
            Assert.That(state.Offers.Length, Is.EqualTo(2));
            Assert.That(state.Items[0].Count, Is.EqualTo(2));
            Assert.That(state.Weapons, Is.EqualTo(new[] { 0 }));
            Assert.That(ShopActionAvailability.CanExecute(state, command, 0), Is.False);
            Assert.That(gateway.TrySend(command, 0, state.Revision), Is.False);
            using var view = new ShopView(new VisualElement(), TryGetIcon);
            using var presenter = new ShopPresenter(gateway, gateway, view);
            presenter.Refresh(0);
            presenter.SubmitSelection();
            Assert.That(arena.CountRequests(), Is.Zero);
            arena.World.EntityManager.DestroyEntity(remote);
            Assert.That(gateway.TryRead(out _), Is.False);
        }

        [Test]
        public void ExplicitLocalTargetKeepsCommandsOnItsOwnConnection()
        {
            using var first = new ShopBridgeTestArena(1);
            using var second = new ShopBridgeTestArena(2);
            second.CreatePlayer(1, false);
            using var gateway = new ClientShopGateway(second.World, 2);
            Assert.That(gateway.TryRead(out ShopState state), Is.True);
            Assert.That(state.CanControl, Is.True);
            Assert.That(gateway.TrySend(ShopCommandKind.Buy, 0, state.Revision), Is.True);
            Assert.That(first.CountRequests(), Is.Zero);
            using EntityQuery requests = second.World.EntityManager.CreateEntityQuery(typeof(SendRpcCommandRequest));
            Assert.That(requests.GetSingleton<SendRpcCommandRequest>().TargetConnection, Is.EqualTo(second.Connection));
        }

        [Test]
        public void RemoteHudReadsOtherPlayerWithoutGrantingRestartControl()
        {
            using var arena = new ShopBridgeTestArena();
            Entity remote = arena.CreatePlayer(4, false);
            arena.World.EntityManager.SetComponentData(remote, new WaveStatus { Phase = WavePhase.Complete });
            using var gateway = new ClientPlayerStatusGateway(arena.World, 4);
            Assert.That(gateway.TryRead(out HudState state), Is.True);
            Assert.That(state.PlayerNumber, Is.EqualTo(4));
            Assert.That(state.IsLocal, Is.False);
            Assert.That(state.Resources, Is.EqualTo(100));
            Assert.That(gateway.TryToggleRestartReadiness(5), Is.False);
            Assert.That(arena.CountRequests(), Is.Zero);
        }

        [TestCase(3)]
        [TestCase(4)]
        public void SharedShopsOccupyDistinctCellsInTwoByTwoGrid(int playerCount)
        {
            var parent = new VisualElement();
            for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
            {
                using var view = new ShopView(parent, TryGetIcon);
                view.SetPlayerLayout(playerIndex, playerCount);
                Assert.That(view.Viewport.style.left.value.value, Is.EqualTo(playerIndex % 2 * 50));
                Assert.That(view.Viewport.style.top.value.value, Is.EqualTo(playerIndex / 2 * 50));
                Assert.That(view.Viewport.style.width.value.value, Is.EqualTo(50));
                Assert.That(view.Viewport.style.height.value.value, Is.EqualTo(50));
            }
        }

        [Test]
        public void FourPlayerHudShowsIndependentHealthAndMarksLocalCharacters()
        {
            var sources = new FakePlayerStatus[4];
            for (int playerIndex = 0; playerIndex < sources.Length; playerIndex++)
            {
                sources[playerIndex] = new FakePlayerStatus();
                sources[playerIndex].Hud.PlayerNumber = sources.Length - playerIndex;
                sources[playerIndex].Hud.IsLocal = playerIndex == 1;
                sources[playerIndex].Hud.Health = 10 * (playerIndex + 1);
            }

            VisualElement root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/CombatHud.uxml").Instantiate();
            VisualTreeAsset playerLayout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/PlayerHud.uxml");
            using var presenter = new HudPresenter(sources, root, () => playerLayout.Instantiate());
            presenter.Refresh();
            VisualElement panels = root.Q("hudPlayers");
            Assert.That(panels.childCount, Is.EqualTo(4));
            for (int playerIndex = 0; playerIndex < sources.Length; playerIndex++)
            {
                Assert.That(panels[playerIndex].Q<Label>("healthText").text, Is.EqualTo(10 * (playerIndex + 1) + " / 100"));
                Assert.That(panels[playerIndex].Q("playerHud").ClassListContains("player-local"), Is.EqualTo(playerIndex == 1));
                Assert.That(panels[playerIndex].ClassListContains("hud-player-slot"), Is.True);
                for (int cornerIndex = 0; cornerIndex < 4; cornerIndex++)
                {
                    bool isPlayerCorner = cornerIndex == sources[playerIndex].Hud.PlayerNumber - 1;
                    Assert.That(panels[playerIndex].ClassListContains("hud-corner-" + cornerIndex), Is.EqualTo(isPlayerCorner));
                }
            }

            sources[2].Available = false;
            presenter.Refresh();
            Assert.That(panels[2].ClassListContains("hidden"), Is.True);
            Assert.That(panels[3].ClassListContains("hidden"), Is.False);
        }

        private static bool TryGetIcon(int catalogId, out Sprite sprite)
        {
            sprite = null;
            return false;
        }
    }
}
