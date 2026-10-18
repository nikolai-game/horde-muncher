using System;
using HordeMuncher.UI.Contracts;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher.Client.Shop
{
    public sealed class ClientShopGateway : IShopStateSource, IShopCommands, IDisposable
    {
        private readonly World _world;
        private readonly int _playerNumber;
        private readonly EntityQuery _players;
        private readonly EntityQuery _connections;
        private readonly EntityQuery _configs;
        private readonly ClientShopNavigationSystem _navigationSystem;
        private ShopNavigationRpc _lastNavigation;
        private double _nextNavigationRefresh;
        private bool _disposed;

        public ClientShopGateway(World world, int playerNumber = 0)
        {
            if (world == null || !world.IsCreated || !world.IsClient())
            {
                throw new ArgumentException("A live client world is required.", nameof(world));
            }

            _world = world;
            _navigationSystem = world.GetOrCreateSystemManaged<ClientShopNavigationSystem>();
            _playerNumber = playerNumber;
            EntityManager entityManager = world.EntityManager;
            _players = entityManager.CreateEntityQuery(typeof(ArenaPlayer),
                typeof(GhostOwner),
                typeof(PlayerProgress),
                typeof(WaveStatus),
                typeof(CombatWallet),
                typeof(CombatHealth),
                typeof(PlayerBonuses),
                typeof(EquippedWeapon),
                typeof(OwnedItem),
                typeof(ShopOffer),
                typeof(UpgradeChoice));
            _connections = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<NetworkStreamInGame>(),
                    ComponentType.ReadOnly<NetworkId>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<NetworkStreamRequestDisconnect>()
                }
            });
            _configs = entityManager.CreateEntityQuery(typeof(EconomyConfig), typeof(CatalogEntry));
        }

        public bool TryRead(out ShopState state)
        {
            state = null;
            if (!TryGetContext(out Entity player, out _))
            {
                return false;
            }

            EntityManager entityManager = _world.EntityManager;
            Entity configEntity = _configs.GetSingletonEntity();
            EconomyConfig config = entityManager.GetComponentData<EconomyConfig>(configEntity);
            PlayerProgress progress = entityManager.GetComponentData<PlayerProgress>(player);
            WaveStatus wave = entityManager.GetComponentData<WaveStatus>(player);
            CombatHealth health = entityManager.GetComponentData<CombatHealth>(player);
            PlayerBonuses bonuses = entityManager.GetComponentData<PlayerBonuses>(player);
            state = new ShopState
            {
                CanControl = IsOwnedPlayer(player),
                Revision = progress.Revision,
                IsOpen = wave.Phase == WavePhase.Shop || wave.Phase == WavePhase.Rewards,
                IsRewardScreen = wave.Phase == WavePhase.Rewards,
                PendingItemRewards = CopyItemRewards(entityManager, player),
                PlayerNumber = entityManager.GetComponentData<GhostOwner>(player).NetworkId,
                WaveNumber = wave.Number,
                Resources = entityManager.GetComponentData<CombatWallet>(player).Resources,
                Level = progress.Level,
                Experience = progress.Xp,
                NextLevelExperience = progress.NextLevelXp,
                PendingUpgrades = progress.PendingUpgrades,
                Ready = progress.Ready,
                Feedback = (ShopFeedback)progress.Result,
                WeaponSlots = config.WeaponSlots,
                RerollPrice = EconomyRules.CalculateShopRerollPrice(config, progress, entityManager.GetBuffer<ShopOffer>(player, true)),
                UpgradeRerollPrice = EconomyRules.CalculateUpgradeRerollPrice(config, progress),
                SellFraction = config.WeaponSellFraction,
                Stats = new ShopStats
                {
                    Health = health.Current,
                    MaximumHealth = health.Maximum,
                    Damage = bonuses.Damage,
                    MeleeDamage = bonuses.MeleeDamage,
                    RangedDamage = bonuses.RangedDamage,
                    CriticalChance = bonuses.CriticalChance,
                    CriticalDamageMultiplier = bonuses.CriticalDamageMultiplier,
                    AttackSpeed = bonuses.AttackSpeed,
                    MoveSpeed = bonuses.MoveSpeed,
                    Range = bonuses.Range,
                    Pickup = bonuses.Pickup,
                    Armor = bonuses.Armor,
                    Luck = bonuses.Luck,
                    Regeneration = bonuses.Regeneration
                },
                Catalog = CopyCatalog(entityManager.GetBuffer<CatalogEntry>(configEntity, true)),
                Offers = CopyOffers(entityManager.GetBuffer<ShopOffer>(player, true)),
                Items = CopyItems(entityManager.GetBuffer<OwnedItem>(player, true)),
                Weapons = CopyWeapons(entityManager.GetBuffer<EquippedWeapon>(player, true)),
                UpgradeChoices = CopyChoices(entityManager.GetBuffer<UpgradeChoice>(player, true))
            };
            if (!state.CanControl && _navigationSystem.TryRead(state.PlayerNumber, out ShopNavigationRpc navigation)
                && navigation.Revision == state.Revision && navigation.WaveNumber == wave.Number && navigation.Phase == wave.Phase)
            {
                state.Navigation = new ShopNavigationState
                {
                    SelectionKey = navigation.SelectionKey.ToString(),
                    ItemPage = navigation.ItemPage,
                    IsShowingStats = navigation.IsShowingStats
                };
            }

            return true;
        }

        public bool TrySendNavigation(ShopNavigationState navigation)
        {
            if (!TryGetContext(out Entity player, out Entity connection) || !IsOwnedPlayer(player))
            {
                return false;
            }

            EntityManager entityManager = _world.EntityManager;
            WaveStatus wave = entityManager.GetComponentData<WaveStatus>(player);
            if (wave.Phase != WavePhase.Shop && wave.Phase != WavePhase.Rewards)
            {
                return false;
            }

            var payload = new ShopNavigationRpc
            {
                PlayerNumber = entityManager.GetComponentData<GhostOwner>(player).NetworkId,
                WaveNumber = wave.Number,
                Phase = wave.Phase,
                Revision = entityManager.GetComponentData<PlayerProgress>(player).Revision,
                SelectionKey = new FixedString64Bytes(navigation.SelectionKey ?? ""),
                ItemPage = navigation.ItemPage,
                IsShowingStats = navigation.IsShowingStats
            };
            double currentTime = _world.Time.ElapsedTime;
            if (payload.Equals(_lastNavigation) && currentTime < _nextNavigationRefresh)
            {
                return true;
            }

            _lastNavigation = payload;
            _nextNavigationRefresh = currentTime + 1;
            Entity request = entityManager.CreateEntity();
            entityManager.AddComponentData(request, payload);
            entityManager.AddComponentData(request, new SendRpcCommandRequest { TargetConnection = connection });
            return true;
        }

        public bool TrySend(ShopCommandKind command, int commandSlot, uint revision)
        {
            if (!TryRead(out ShopState state) || !state.CanControl || state.Revision != revision
                || !ShopActionAvailability.CanExecute(state, command, commandSlot))
            {
                return false;
            }

            if (!TryGetContext(out _, out Entity connection) || !TryMapCommand(command, out ShopAction action))
            {
                return false;
            }

            EntityManager entityManager = _world.EntityManager;
            Entity request = entityManager.CreateEntity();
            entityManager.AddComponentData(request, new ShopRequest
            {
                Action = action,
                Slot = commandSlot,
                Revision = revision
            });
            entityManager.AddComponentData(request, new SendRpcCommandRequest
            {
                TargetConnection = connection
            });
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_world.IsCreated)
            {
                _players.Dispose();
                _connections.Dispose();
                _configs.Dispose();
            }
        }

        private bool TryGetContext(out Entity player, out Entity connection)
        {
            player = Entity.Null;
            connection = Entity.Null;
            if (_disposed || !_world.IsCreated || _players.IsEmptyIgnoreFilter || _connections.CalculateEntityCount() != 1
                || _configs.CalculateEntityCount() != 1)
            {
                return false;
            }

            connection = _connections.GetSingletonEntity();
            EntityManager entityManager = _world.EntityManager;
            int localNumber = entityManager.GetComponentData<NetworkId>(connection).Value;
            int targetNumber = _playerNumber > 0 ? _playerNumber : localNumber;
            using NativeArray<Entity> players = _players.ToEntityArray(Allocator.Temp);
            foreach (Entity candidate in players)
            {
                if (entityManager.GetComponentData<GhostOwner>(candidate).NetworkId != targetNumber)
                {
                    continue;
                }

                if (player != Entity.Null)
                {
                    return false;
                }

                player = candidate;
            }

            return targetNumber > 0 && player != Entity.Null && (_playerNumber > 0 || IsOwnedPlayer(player));
        }

        private bool IsOwnedPlayer(Entity player)
        {
            EntityManager manager = _world.EntityManager;
            return manager.HasComponent<GhostOwnerIsLocal>(player) && manager.IsComponentEnabled<GhostOwnerIsLocal>(player)
                && manager.GetComponentData<GhostOwner>(player).NetworkId == _connections.GetSingleton<NetworkId>().Value;
        }

        private static bool TryMapCommand(ShopCommandKind command, out ShopAction action)
        {
            action = default;
            switch (command)
            {
                case ShopCommandKind.TakeItemReward:
                    action = ShopAction.TakeItemReward;
                    return true;
                case ShopCommandKind.RecycleItemReward:
                    action = ShopAction.RecycleItemReward;
                    return true;
                case ShopCommandKind.Buy:
                    action = ShopAction.Buy;
                    return true;
                case ShopCommandKind.Reroll:
                    action = ShopAction.Reroll;
                    return true;
                case ShopCommandKind.RerollUpgrades:
                    action = ShopAction.RerollUpgrades;
                    return true;
                case ShopCommandKind.Lock:
                    action = ShopAction.Lock;
                    return true;
                case ShopCommandKind.Ready:
                    action = ShopAction.Ready;
                    return true;
                case ShopCommandKind.Upgrade:
                    action = ShopAction.Upgrade;
                    return true;
                case ShopCommandKind.Merge:
                    action = ShopAction.Merge;
                    return true;
                case ShopCommandKind.Sell:
                    action = ShopAction.Sell;
                    return true;
                default:
                    return false;
            }
        }

        private static ShopProduct[] CopyCatalog(DynamicBuffer<CatalogEntry> catalog)
        {
            ShopProduct[] products = new ShopProduct[catalog.Length];
            for (int catalogIndex = 0; catalogIndex < catalog.Length; catalogIndex++)
            {
                CatalogEntry entry = catalog[catalogIndex];
                products[catalogIndex] = new ShopProduct
                {
                    Name = entry.Name.ToString(),
                    Rarity = entry.Kind == GoodsKind.Weapon ? entry.Rarity.ToString() : "",
                    UpgradeCatalogId = entry.UpgradeCatalogId,
                    Description = entry.Description.ToString(),
                    IsWeapon = entry.Kind == GoodsKind.Weapon,
                    IsShotgun = entry.Weapon == WeaponKind.Shotgun,
                    IsMelee = WeaponCombatRules.IsMelee(entry.Weapon),
                    Bonus = (ShopBonusKind)entry.Bonus,
                    Amount = entry.Amount,
                    Penalty = (ShopBonusKind)entry.Penalty,
                    PenaltyAmount = entry.PenaltyAmount,
                    Price = entry.Price,
                    Damage = entry.Damage,
                    Targets = entry.Targets,
                    Interval = entry.Interval,
                    Range = entry.Range,
                    Cone = entry.Cone,
                    ExtraPierceTargets = entry.ExtraPierceTargets,
                    KnockbackDistance = entry.KnockbackDistance,
                    RetargetOnKill = entry.RetargetOnKill
                };
            }

            return products;
        }

        private static int[] CopyItemRewards(EntityManager entityManager, Entity player)
        {
            if (!entityManager.HasBuffer<PendingItemReward>(player))
            {
                return Array.Empty<int>();
            }

            DynamicBuffer<PendingItemReward> rewards = entityManager.GetBuffer<PendingItemReward>(player, true);
            int[] catalogIds = new int[rewards.Length];
            for (int rewardIndex = 0; rewardIndex < rewards.Length; rewardIndex++)
            {
                catalogIds[rewardIndex] = rewards[rewardIndex].CatalogId;
            }

            return catalogIds;
        }

        private static ShopOfferState[] CopyOffers(DynamicBuffer<ShopOffer> offers)
        {
            ShopOfferState[] result = new ShopOfferState[offers.Length];
            for (int offerSlot = 0; offerSlot < offers.Length; offerSlot++)
            {
                ShopOffer offer = offers[offerSlot];
                result[offerSlot] = new ShopOfferState
                {
                    CatalogId = offer.CatalogId,
                    Price = offer.Price,
                    Locked = offer.Locked,
                    Sold = offer.Sold
                };
            }

            return result;
        }

        private static ShopOwnedItem[] CopyItems(DynamicBuffer<OwnedItem> items)
        {
            ShopOwnedItem[] result = new ShopOwnedItem[items.Length];
            for (int ownedItemIndex = 0; ownedItemIndex < items.Length; ownedItemIndex++)
            {
                OwnedItem item = items[ownedItemIndex];
                result[ownedItemIndex] = new ShopOwnedItem
                {
                    CatalogId = item.CatalogId,
                    Count = item.Count
                };
            }

            return result;
        }

        private static int[] CopyWeapons(DynamicBuffer<EquippedWeapon> weapons)
        {
            int[] result = new int[weapons.Length];
            for (int weaponSlot = 0; weaponSlot < weapons.Length; weaponSlot++)
            {
                result[weaponSlot] = weapons[weaponSlot].CatalogId;
            }

            return result;
        }

        private static ShopUpgradeChoice[] CopyChoices(DynamicBuffer<UpgradeChoice> choices)
        {
            var result = new ShopUpgradeChoice[choices.Length];
            for (int upgradeChoiceSlot = 0; upgradeChoiceSlot < choices.Length; upgradeChoiceSlot++)
            {
                UpgradeChoice choice = choices[upgradeChoiceSlot];
                result[upgradeChoiceSlot] = new ShopUpgradeChoice
                {
                    Stat = (ShopBonusKind)choice.Stat,
                    Rarity = (int)choice.Rarity,
                    Points = choice.Points
                };
            }

            return result;
        }
    }
}
