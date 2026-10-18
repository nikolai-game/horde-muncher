using System;
using HordeMuncher.Client.Shop;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher.Tests.Shop
{
    public sealed class ShopBridgeTestArena : IDisposable
    {
        public World World { get; }
        public Entity Player { get; }
        public Entity Connection { get; }
        public Entity Config { get; }
        public ClientShopGateway Gateway { get; }

        public ShopBridgeTestArena(int networkId = 1, bool isServer = false)
        {
            World = new World("Shop bridge tests", isServer ? WorldFlags.GameServer : WorldFlags.GameClient);
            EntityManager entityManager = World.EntityManager;
            Connection = entityManager.CreateEntity(typeof(NetworkId), typeof(NetworkStreamInGame));
            entityManager.SetComponentData(Connection, new NetworkId
            {
                Value = networkId
            });
            Config = entityManager.CreateEntity(typeof(EconomyConfig));
            entityManager.SetComponentData(Config, new EconomyConfig
            {
                WeaponSlots = 6,
                RerollPrice = 2,
                RerollGrowth = 1,
                UpgradeRerollPrice = 2,
                UpgradeRerollGrowth = 1,
                EnemyPriceGrowth = 0.25f,
                WeaponSellFraction = 0.5f,
                FirstLevelXp = 5
            });
            entityManager.AddBuffer<CatalogEntry>(Config).Add(new CatalogEntry
            {
                Name = "Pistol",
                Kind = GoodsKind.Weapon,
                Weapon = WeaponKind.Pistol,
                Price = 12,
                Weight = 3,
                Damage = 15,
                Interval = 0.25f,
                Range = 9
            });
            entityManager.GetBuffer<CatalogEntry>(Config).Add(new CatalogEntry
            {
                Name = "Heart",
                Kind = GoodsKind.Item,
                Price = 8,
                Weight = 2,
                Bonus = BonusKind.Health,
                Amount = 20
            });
            Player = CreatePlayer(networkId, true);
            if (!isServer)
            {
                Gateway = new ClientShopGateway(World);
            }
        }

        public Entity CreatePlayer(int networkId, bool local)
        {
            EntityManager entityManager = World.EntityManager;
            Entity player = entityManager.CreateEntity(typeof(ArenaPlayer),
                typeof(GhostOwner),
                typeof(GhostOwnerIsLocal),
                typeof(PlayerProgress),
                typeof(WaveStatus),
                typeof(CombatWallet),
                typeof(CombatHealth),
                typeof(PlayerBonuses));
            entityManager.SetComponentEnabled<GhostOwnerIsLocal>(player, local);
            entityManager.SetComponentData(player, new GhostOwner
            {
                NetworkId = networkId
            });
            entityManager.SetComponentData(player, new PlayerProgress
            {
                Initialized = true,
                Revision = 5,
                Level = 1,
                NextLevelXp = 5,
                ShopWave = 0
            });
            entityManager.SetComponentData(player, new WaveStatus
            {
                Phase = WavePhase.Shop,
                Number = 1
            });
            entityManager.SetComponentData(player, new CombatWallet
            {
                Resources = 100
            });
            entityManager.SetComponentData(player, new CombatHealth
            {
                Current = 100,
                Maximum = 100
            });
            entityManager.AddBuffer<EquippedWeapon>(player).Add(new EquippedWeapon
            {
                CatalogId = 0
            });
            entityManager.AddBuffer<OwnedItem>(player).Add(new OwnedItem
            {
                CatalogId = 1,
                Count = 2
            });
            entityManager.AddBuffer<ShopOffer>(player).Add(new ShopOffer
            {
                CatalogId = 0,
                Price = 12
            });
            entityManager.GetBuffer<ShopOffer>(player).Add(new ShopOffer
            {
                CatalogId = 1,
                Price = 8
            });
            entityManager.AddBuffer<UpgradeChoice>(player).Add(new UpgradeChoice
            {
                Stat = BonusKind.Damage,
                Points = 5
            });
            return player;
        }

        public int CountRequests()
        {
            using EntityQuery query = World.EntityManager.CreateEntityQuery(typeof(ShopRequest), typeof(SendRpcCommandRequest));
            return query.CalculateEntityCount();
        }

        public void Dispose()
        {
            Gateway?.Dispose();
            if (World.IsCreated)
            {
                World.Dispose();
            }
        }
    }
}
