using HordeMuncher.Client.Shop;
using HordeMuncher.UI.Contracts;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher.Tests.Shop
{
    public sealed class ClientShopGatewayTests
    {
        [TestCase(0, 0, 0)]
        [TestCase(0, 12, 12)]
        [TestCase(0, 65, 65)]
        [TestCase(1, 12, 12)]
        public void EnteringShopPreservesEarnedResourcesWithoutGrantingMoney(int waveIndex, int resources, int expectedResources)
        {
            using var arena = new ShopBridgeTestArena(isServer: true);
            EntityManager manager = arena.World.EntityManager;
            PlayerProgress progress = manager.GetComponentData<PlayerProgress>(arena.Player);
            progress.ShopWave = -1;
            manager.SetComponentData(arena.Player, progress);
            manager.SetComponentData(arena.Player, new CombatWallet { Resources = resources, BankedResources = 7 });
            Entity wave = manager.CreateEntity(typeof(WaveProgress));
            manager.SetComponentData(wave, new WaveProgress { Index = waveIndex, Phase = WavePhase.Shop });
            ShopSystem shop = arena.World.GetOrCreateSystemManaged<ShopSystem>();
            shop.Update();

            CombatWallet wallet = manager.GetComponentData<CombatWallet>(arena.Player);
            Assert.That(wallet.Resources, Is.EqualTo(expectedResources));
            Assert.That(wallet.BankedResources, Is.EqualTo(7));
            int resourcesAfterPurchase = expectedResources >= 8 ? expectedResources - 8 : 0;
            wallet.Resources = resourcesAfterPurchase;
            manager.SetComponentData(arena.Player, wallet);
            shop.Update();
            Assert.That(manager.GetComponentData<CombatWallet>(arena.Player).Resources, Is.EqualTo(resourcesAfterPurchase));
        }

        [Test]
        public void NavigationIsRelayedWithAuthenticatedOwnerAndReadOnlyByObserver()
        {
            using var owner = new ShopBridgeTestArena();
            Assert.That(owner.Gateway.TrySendNavigation(new ShopNavigationState
            {
                SelectionKey = "command:Buy:1",
                ItemPage = 1,
                IsShowingStats = true
            }), Is.True);
            using EntityQuery sent = owner.World.EntityManager.CreateEntityQuery(typeof(ShopNavigationRpc));
            ShopNavigationRpc payload = sent.GetSingleton<ShopNavigationRpc>();
            payload.PlayerNumber = 99;
            using var server = new ShopBridgeTestArena(isServer: true);
            Entity request = server.World.EntityManager.CreateEntity(typeof(ShopNavigationRpc), typeof(ReceiveRpcCommandRequest));
            server.World.EntityManager.SetComponentData(request, payload);
            server.World.EntityManager.SetComponentData(request, new ReceiveRpcCommandRequest { SourceConnection = server.Connection });
            server.World.CreateSystemManaged<ShopNavigationRelaySystem>().Update();
            using EntityQuery relayed = server.World.EntityManager.CreateEntityQuery(typeof(ShopNavigationRpc), typeof(SendRpcCommandRequest));
            payload = relayed.GetSingleton<ShopNavigationRpc>();
            Assert.That(payload.PlayerNumber, Is.EqualTo(1));

            using var observer = new ShopBridgeTestArena(2);
            Entity remotePlayer = observer.CreatePlayer(1, false);
            using var remoteGateway = new ClientShopGateway(observer.World, 1);
            Entity received = observer.World.EntityManager.CreateEntity(typeof(ShopNavigationRpc), typeof(ReceiveRpcCommandRequest));
            observer.World.EntityManager.SetComponentData(received, payload);
            observer.World.GetExistingSystemManaged<ClientShopNavigationSystem>().Update();
            Assert.That(remoteGateway.TryRead(out ShopState state), Is.True);
            Assert.That(state.CanControl, Is.False);
            Assert.That(state.Navigation.SelectionKey, Is.EqualTo("command:Buy:1"));
            Assert.That(state.Navigation.IsShowingStats, Is.True);
            Assert.That(state.Navigation.ItemPage, Is.EqualTo(1));
            Assert.That(remoteGateway.TrySendNavigation(state.Navigation), Is.False);
            PlayerProgress progress = observer.World.EntityManager.GetComponentData<PlayerProgress>(remotePlayer);
            progress.Revision++;
            observer.World.EntityManager.SetComponentData(remotePlayer, progress);
            remoteGateway.TryRead(out state);
            Assert.That(state.Navigation.SelectionKey, Is.Null);
        }

        [Test]
        public void NavigationFromPreviousShopRevisionIsDiscardedByServer()
        {
            using var server = new ShopBridgeTestArena(isServer: true);
            Entity request = server.World.EntityManager.CreateEntity(typeof(ShopNavigationRpc), typeof(ReceiveRpcCommandRequest));
            server.World.EntityManager.SetComponentData(request, new ShopNavigationRpc
            {
                Revision = 4,
                WaveNumber = 1,
                Phase = WavePhase.Shop
            });
            server.World.EntityManager.SetComponentData(request, new ReceiveRpcCommandRequest { SourceConnection = server.Connection });
            server.World.CreateSystemManaged<ShopNavigationRelaySystem>().Update();
            using EntityQuery relayed = server.World.EntityManager.CreateEntityQuery(typeof(ShopNavigationRpc));
            Assert.That(relayed.IsEmptyIgnoreFilter, Is.True);
        }

        [Test]
        public void SnapshotContainsOnlyTheLocalOwnerAndCopiesBuffers()
        {
            using var arena = new ShopBridgeTestArena();
            arena.CreatePlayer(2, false);
            Assert.That(arena.Gateway.TryRead(out ShopState state), Is.True);
            Assert.That(state.PlayerNumber, Is.EqualTo(1));
            Assert.That(state.Catalog[0].Name, Is.EqualTo("Pistol"));
            Assert.That(state.Items[0].Count, Is.EqualTo(2));
            arena.World.EntityManager.GetBuffer<ShopOffer>(arena.Player).Clear();
            Assert.That(state.Offers.Length, Is.EqualTo(2));
            state.Catalog[0] = new ShopProduct
            {
                Name = "Changed"
            };
            Assert.That(arena.World.EntityManager.GetBuffer<CatalogEntry>(arena.Config)[0].Name.ToString(), Is.EqualTo("Pistol"));
        }

        [Test]
        public void CommandCreatesVersionedRpcOnTheOwnedConnectionWithoutMutatingGameState()
        {
            using var arena = new ShopBridgeTestArena();
            Assert.That(arena.Gateway.TrySend(ShopCommandKind.Buy, 0, 5), Is.True);
            using EntityQuery requests = arena.World.EntityManager.CreateEntityQuery(typeof(ShopRequest), typeof(SendRpcCommandRequest));
            Assert.That(requests.CalculateEntityCount(), Is.EqualTo(1));
            ShopRequest request = requests.GetSingleton<ShopRequest>();
            Assert.That(request.Action, Is.EqualTo(ShopAction.Buy));
            Assert.That(request.Slot, Is.Zero);
            Assert.That(request.Revision, Is.EqualTo(5));
            Assert.That(requests.GetSingleton<SendRpcCommandRequest>().TargetConnection, Is.EqualTo(arena.Connection));
            Assert.That(arena.World.EntityManager.GetComponentData<CombatWallet>(arena.Player).Resources, Is.EqualTo(100));
            Assert.That(arena.World.EntityManager.GetBuffer<ShopOffer>(arena.Player)[0].Sold, Is.False);
        }

        [Test]
        public void LockedWeaponOfferSurvivesRerollAndUnlockAllowsRefresh()
        {
            using var arena = new ShopBridgeTestArena(isServer: true);
            EntityManager manager = arena.World.EntityManager;
            manager.SetComponentData(arena.Player, new PlayerProgress { Revision = 5, RandomState = 17, PriceMultiplier = 1 });
            EconomyConfig config = manager.GetComponentData<EconomyConfig>(arena.Config);
            DynamicBuffer<CatalogEntry> catalog = manager.GetBuffer<CatalogEntry>(arena.Config);
            var lockRequest = new ShopRequest { Action = ShopAction.Lock, Slot = 0, Revision = 5 };
            Assert.That(EconomyRules.ApplyShopRequest(manager, arena.Player, lockRequest, config, catalog), Is.EqualTo(ShopResult.Success));
            ShopOffer lockedOffer = manager.GetBuffer<ShopOffer>(arena.Player)[0];
            Assert.That(lockedOffer.Locked, Is.True);
            Assert.That(manager.GetComponentData<CombatWallet>(arena.Player).Resources, Is.EqualTo(100));

            var rerollRequest = new ShopRequest { Action = ShopAction.Reroll, Revision = 6 };
            Assert.That(EconomyRules.ApplyShopRequest(manager, arena.Player, rerollRequest, config, catalog), Is.EqualTo(ShopResult.Success));
            ShopOffer retainedOffer = manager.GetBuffer<ShopOffer>(arena.Player)[0];
            Assert.That(retainedOffer.CatalogId, Is.EqualTo(lockedOffer.CatalogId));
            Assert.That(retainedOffer.Price, Is.EqualTo(lockedOffer.Price));
            Assert.That(retainedOffer.Locked, Is.True);
            Assert.That(manager.GetComponentData<CombatWallet>(arena.Player).Resources, Is.EqualTo(98));

            lockRequest.Revision = 7;
            Assert.That(EconomyRules.ApplyShopRequest(manager, arena.Player, lockRequest, config, catalog), Is.EqualTo(ShopResult.Success));
            Assert.That(manager.GetBuffer<ShopOffer>(arena.Player)[0].Locked, Is.False);
            rerollRequest.Revision = 8;
            Assert.That(EconomyRules.ApplyShopRequest(manager, arena.Player, rerollRequest, config, catalog), Is.EqualTo(ShopResult.Success));
            Assert.That(manager.GetBuffer<ShopOffer>(arena.Player)[0].Locked, Is.False);
        }

        [TestCase(ShopCommandKind.Buy, -1, 5u)]
        [TestCase(ShopCommandKind.Buy, 2, 5u)]
        [TestCase(ShopCommandKind.Buy, 0, 4u)]
        [TestCase(ShopCommandKind.Sell, 0, 5u)]
        [TestCase((ShopCommandKind)99, 0, 5u)]
        public void InvalidStaleOrDisallowedCommandsDoNotCreateRpc(ShopCommandKind command, int commandSlot, uint revision)
        {
            using var arena = new ShopBridgeTestArena();
            Assert.That(arena.Gateway.TrySend(command, commandSlot, revision), Is.False);
            Assert.That(arena.CountRequests(), Is.Zero);
        }

        [Test]
        public void OwnershipDisconnectAndAmbiguousConnectionsAreRejected()
        {
            using var arena = new ShopBridgeTestArena();
            EntityManager entityManager = arena.World.EntityManager;
            entityManager.SetComponentData(arena.Player, new GhostOwner
            {
                NetworkId = 2
            });
            Assert.That(arena.Gateway.TryRead(out _), Is.False);
            Assert.That(arena.Gateway.TrySend(ShopCommandKind.Buy, 0, 5), Is.False);
            entityManager.SetComponentData(arena.Player, new GhostOwner
            {
                NetworkId = 1
            });
            Entity extra = entityManager.CreateEntity(typeof(NetworkId), typeof(NetworkStreamInGame));
            Assert.That(arena.Gateway.TryRead(out _), Is.False);
            entityManager.DestroyEntity(extra);
            entityManager.AddComponent<NetworkStreamRequestDisconnect>(arena.Connection);
            Assert.That(arena.Gateway.TryRead(out _), Is.False);
            Assert.That(arena.CountRequests(), Is.Zero);
        }

        [Test]
        public void PhaseAndUpgradeGatesAreCheckedAgainAtTheBridgeBoundary()
        {
            using var arena = new ShopBridgeTestArena();
            EntityManager entityManager = arena.World.EntityManager;
            entityManager.SetComponentData(arena.Player, new WaveStatus
            {
                Phase = WavePhase.Spawning
            });
            Assert.That(arena.Gateway.TrySend(ShopCommandKind.Ready, 0, 5), Is.False);
            entityManager.SetComponentData(arena.Player, new WaveStatus
            {
                Phase = WavePhase.Shop,
                Number = 1
            });
            PlayerProgress progress = entityManager.GetComponentData<PlayerProgress>(arena.Player);
            progress.PendingUpgrades = 1;
            entityManager.SetComponentData(arena.Player, progress);
            Assert.That(arena.Gateway.TrySend(ShopCommandKind.Buy, 0, 5), Is.False);
            Assert.That(arena.Gateway.TrySend(ShopCommandKind.Upgrade, 0, 5), Is.True);
        }

        [Test]
        public void MultipleLocalGhostsAreRejectedInsteadOfChoosingAnArbitraryPlayer()
        {
            using var arena = new ShopBridgeTestArena();
            arena.CreatePlayer(1, true);
            Assert.That(arena.Gateway.TryRead(out _), Is.False);
        }

        [Test]
        public void TwoClientWorldsKeepCommandsAndInventoriesSeparate()
        {
            using var first = new ShopBridgeTestArena(1);
            using var second = new ShopBridgeTestArena(2);
            Assert.That(second.Gateway.TrySend(ShopCommandKind.Buy, 1, 5), Is.True);
            Assert.That(first.CountRequests(), Is.Zero);
            Assert.That(second.CountRequests(), Is.EqualTo(1));
            Assert.That(first.Gateway.TryRead(out ShopState firstState), Is.True);
            Assert.That(second.Gateway.TryRead(out ShopState secondState), Is.True);
            Assert.That(firstState.PlayerNumber, Is.EqualTo(1));
            Assert.That(secondState.PlayerNumber, Is.EqualTo(2));
        }

        [Test]
        public void DespawnDisposedBridgeAndDestroyedWorldAreSafe()
        {
            using (var arena = new ShopBridgeTestArena())
            {
                arena.World.EntityManager.DestroyEntity(arena.Player);
                Assert.That(arena.Gateway.TryRead(out _), Is.False);
                arena.Gateway.Dispose();
                Assert.That(arena.Gateway.TryRead(out _), Is.False);
            }

            using (var arena = new ShopBridgeTestArena())
            {
                arena.World.Dispose();
                Assert.That(arena.Gateway.TryRead(out _), Is.False);
                Assert.That(arena.Gateway.TrySend(ShopCommandKind.Buy, 0, 5), Is.False);
            }
        }

        [Test]
        public void RpcPayloadIsAcceptedByServerRulesAndReplayDoesNotChargeTwice()
        {
            using var client = new ShopBridgeTestArena();
            Assert.That(client.Gateway.TrySend(ShopCommandKind.Buy, 0, 5), Is.True);
            using EntityQuery outgoing = client.World.EntityManager.CreateEntityQuery(typeof(ShopRequest));
            ShopRequest payload = outgoing.GetSingleton<ShopRequest>();
            using var server = new ShopBridgeTestArena(isServer: true);
            EntityManager entityManager = server.World.EntityManager;
            entityManager.AddComponentData(server.Config, new WaveProgress
            {
                Phase = WavePhase.Shop
            });
            for (int requestIndex = 0; requestIndex < 2; requestIndex++)
            {
                Entity received = entityManager.CreateEntity(typeof(ShopRequest), typeof(ReceiveRpcCommandRequest));
                entityManager.SetComponentData(received, payload);
                entityManager.SetComponentData(received, new ReceiveRpcCommandRequest
                {
                    SourceConnection = server.Connection
                });
            }

            ShopSystem shop = server.World.CreateSystemManaged<ShopSystem>();
            server.World.SetTime(new TimeData(0.1, 0.1f));
            shop.Update();
            Assert.That(entityManager.GetComponentData<CombatWallet>(server.Player).Resources, Is.EqualTo(88));
            Assert.That(entityManager.GetBuffer<EquippedWeapon>(server.Player).Length, Is.EqualTo(2));
            Assert.That(entityManager.GetComponentData<PlayerProgress>(server.Player).Revision, Is.EqualTo(6));
        }
    }
}
