using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher.Tests
{
    public sealed class ShopCommandDeliveryTests
    {
        [Test]
        public void DuplicateDelayedAndDisconnectedRequestsCannotSpendTwice()
        {
            using var world = new World("Adversarial shop delivery");
            EntityManager entityManager = world.EntityManager;
            Entity configuration = entityManager.CreateEntity(typeof(EconomyConfig), typeof(WaveProgress));
            entityManager.SetComponentData(configuration, new EconomyConfig { StartingResources = 100, StartingWeapon = -1, FirstLevelXp = 5 });
            entityManager.SetComponentData(configuration, new WaveProgress { Phase = WavePhase.Shop });
            entityManager.AddBuffer<CatalogEntry>(configuration).Add(new CatalogEntry { Kind = GoodsKind.Item, Price = 10, Weight = 1 });
            Entity player = entityManager.CreateEntity(typeof(ArenaPlayer), typeof(GhostOwner), typeof(PlayerProgress),
                typeof(PlayerBonuses), typeof(CombatWallet), typeof(CombatHealth));
            entityManager.SetComponentData(player, new GhostOwner { NetworkId = 7 });
            entityManager.AddBuffer<EquippedWeapon>(player);
            entityManager.AddBuffer<OwnedItem>(player);
            entityManager.AddBuffer<ShopOffer>(player);
            entityManager.AddBuffer<UpgradeChoice>(player);
            Entity connection = entityManager.CreateEntity(typeof(NetworkId), typeof(LobbyMember), typeof(NetworkStreamInGame));
            entityManager.SetComponentData(connection, new NetworkId { Value = 7 });
            ShopSystem shop = world.CreateSystemManaged<ShopSystem>();
            world.SetTime(new TimeData(0.1, 0.1f));
            shop.Update();
            uint revision = entityManager.GetComponentData<PlayerProgress>(player).Revision;
            DynamicBuffer<ShopOffer> offers = entityManager.GetBuffer<ShopOffer>(player);
            offers[0] = new ShopOffer { CatalogId = 0, Price = 10 };
            entityManager.RemoveComponent<LobbyMember>(connection);
            QueueBuy(entityManager, connection, revision);
            shop.Update();
            Assert.That(entityManager.GetComponentData<CombatWallet>(player).Resources, Is.EqualTo(100));
            entityManager.AddComponent<LobbyMember>(connection);
            entityManager.SetComponentData(configuration, new WaveProgress { Phase = WavePhase.Spawning });
            QueueBuy(entityManager, connection, revision);
            shop.Update();
            Assert.That(entityManager.GetComponentData<CombatWallet>(player).Resources, Is.EqualTo(100));
            entityManager.SetComponentData(configuration, new WaveProgress { Phase = WavePhase.Shop });
            QueueBuy(entityManager, connection, revision);
            QueueBuy(entityManager, connection, revision);
            shop.Update();
            AssertPurchase(entityManager, player, revision + 1);

            world.SetTime(new TimeData(2, 0.1f));
            QueueBuy(entityManager, connection, revision);
            shop.Update();
            AssertPurchase(entityManager, player, revision + 1);

            offers = entityManager.GetBuffer<ShopOffer>(player);
            offers[0] = new ShopOffer { CatalogId = 0, Price = 10 };
            entityManager.AddComponent<NetworkStreamRequestDisconnect>(connection);
            QueueBuy(entityManager, connection, revision + 1);
            shop.Update();
            AssertPurchase(entityManager, player, revision + 1);
            using EntityQuery requests = entityManager.CreateEntityQuery(typeof(ShopRequest));
            Assert.That(requests.IsEmptyIgnoreFilter, Is.True, "Rejected commands must also be consumed");
        }

        private static void QueueBuy(EntityManager entityManager, Entity connection, uint revision)
        {
            Entity request = entityManager.CreateEntity(typeof(ShopRequest), typeof(ReceiveRpcCommandRequest));
            entityManager.SetComponentData(request, new ShopRequest { Action = ShopAction.Buy, Slot = 0, Revision = revision });
            entityManager.SetComponentData(request, new ReceiveRpcCommandRequest { SourceConnection = connection });
        }

        private static void AssertPurchase(EntityManager entityManager, Entity player, uint revision)
        {
            Assert.That(entityManager.GetComponentData<CombatWallet>(player).Resources, Is.EqualTo(90));
            Assert.That(entityManager.GetComponentData<PlayerProgress>(player).Revision, Is.EqualTo(revision));
            Assert.That(entityManager.GetBuffer<OwnedItem>(player).Length, Is.EqualTo(1));
        }
    }
}
