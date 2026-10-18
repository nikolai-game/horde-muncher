using System;
using Unity.Core;
using Unity.Entities;
using Unity.NetCode;
using UnityEditor;
using UnityEngine;

namespace HordeMuncher.Editor
{
    public static class EconomyVerification
    {
        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        [MenuItem("Horde Muncher/Verify Economy Rules")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            using var world = new World("Economy checks");
            EntityManager entityManager = world.EntityManager;
            var settings = new EconomyConfig
            {
                WeaponSlots = 6,
                StartingWeapon = 0,
                StartingResources = 100,
                FirstLevelXp = 5,
                LevelXpGrowth = 5,
                RerollPrice = 2,
                RerollGrowth = 1
            };
            Entity configurationEntity = entityManager.CreateEntity(typeof(EconomyConfig), typeof(WaveProgress));
            entityManager.SetComponentData(configurationEntity, settings);
            entityManager.SetComponentData(configurationEntity, new WaveProgress
            {
                Phase = WavePhase.Shop
            });
            entityManager.AddBuffer<CatalogEntry>(configurationEntity);
            foreach (CatalogEntry entry in EconomyProjectSetup.DefaultCatalog())
            {
                entityManager.GetBuffer<CatalogEntry>(configurationEntity).Add(entry);
            }

            Entity player = entityManager.CreateEntity(typeof(ArenaPlayer),
                typeof(GhostOwner),
                typeof(PlayerProgress),
                typeof(PlayerBonuses),
                typeof(CombatWallet),
                typeof(CombatHealth));
            entityManager.SetComponentData(player, new GhostOwner
            {
                NetworkId = 1
            });
            entityManager.SetComponentData(player, new CombatHealth
            {
                Current = 100,
                Maximum = 100
            });
            entityManager.AddBuffer<EquippedWeapon>(player);
            entityManager.AddBuffer<OwnedItem>(player);
            entityManager.AddBuffer<ShopOffer>(player);
            entityManager.AddBuffer<UpgradeChoice>(player);
            Entity connection = entityManager.CreateEntity(typeof(NetworkId));
            entityManager.SetComponentData(connection, new NetworkId
            {
                Value = 1
            });
            ShopSystem system = world.CreateSystemManaged<ShopSystem>();
            double elapsedTime = 0;
            void Tick()
            {
                elapsedTime += 0.1;
                world.SetTime(new TimeData(elapsedTime, 0.1f));
                system.Update();
            }

            void ApplyShopAction(ShopAction action, int commandSlot = 0, uint? revision = null, Entity? sourceConnection = null)
            {
                Entity requestEntity = entityManager.CreateEntity(typeof(ShopRequest), typeof(ReceiveRpcCommandRequest));
                entityManager.SetComponentData(requestEntity, new ShopRequest
                {
                    Action = action,
                    Slot = commandSlot,
                    Revision = revision ?? entityManager.GetComponentData<PlayerProgress>(player).Revision
                });
                entityManager.SetComponentData(requestEntity, new ReceiveRpcCommandRequest
                {
                    SourceConnection = sourceConnection ?? connection
                });
                Tick();
            }

            void SetFirstShopOffer(int catalogId, int price = 1)
            {
                DynamicBuffer<ShopOffer> offers = entityManager.GetBuffer<ShopOffer>(player);
                offers[0] = new ShopOffer
                {
                    CatalogId = catalogId,
                    Price = price
                };
            }

            Tick();
            Check(entityManager.GetBuffer<EquippedWeapon>(player).Length == 1 && entityManager.GetBuffer<ShopOffer>(player).Length == 4,
                "Initial weapon/offers missing");
            int healthItemCatalogId = EconomyProjectSetup.DefaultWeapons().Length;
            SetFirstShopOffer(healthItemCatalogId, 8);
            uint version = entityManager.GetComponentData<PlayerProgress>(player).Revision;
            ApplyShopAction(ShopAction.Buy, revision: version);
            ApplyShopAction(ShopAction.Buy, revision: version);
            Check(entityManager.GetComponentData<CombatWallet>(player).Resources == 92
                && entityManager.GetComponentData<CombatHealth>(player).Maximum == 106, "Duplicate buy or health item failed");
            SetFirstShopOffer(healthItemCatalogId, 999);
            ApplyShopAction(ShopAction.Buy);
            Check(entityManager.GetComponentData<CombatWallet>(player).Resources == 92
                && !entityManager.GetBuffer<ShopOffer>(player)[0].Sold, "Unaffordable purchase accepted");
            SetFirstShopOffer(1);
            ApplyShopAction(ShopAction.Lock);
            int locked = entityManager.GetBuffer<ShopOffer>(player)[0].CatalogId;
            ApplyShopAction(ShopAction.Reroll);
            Check(entityManager.GetBuffer<ShopOffer>(player)[0].Locked
                && entityManager.GetBuffer<ShopOffer>(player)[0].CatalogId == locked, "Reroll replaced locked offer");
            for (int purchaseIndex = 0; purchaseIndex < 5; purchaseIndex++)
            {
                SetFirstShopOffer(0);
                ApplyShopAction(ShopAction.Buy);
            }

            int money = entityManager.GetComponentData<CombatWallet>(player).Resources;
            SetFirstShopOffer(1);
            ApplyShopAction(ShopAction.Buy);
            Check(entityManager.GetBuffer<EquippedWeapon>(player).Length == 6
                && entityManager.GetComponentData<CombatWallet>(player).Resources == money, "Weapon slot cap failed");
            EconomyRules.AwardExperience(entityManager, player, 19, settings);
            PlayerProgress playerProgress = entityManager.GetComponentData<PlayerProgress>(player);
            Check(playerProgress.Level == 3 && playerProgress.Xp == 4 && playerProgress.PendingUpgrades == 2,
                "XP carry/multiple levels failed");
            EconomyRules.GenerateUpgradeChoices(entityManager,
                player,
                ref playerProgress);
            entityManager.SetComponentData(player, playerProgress);
            DynamicBuffer<UpgradeChoice> upgrades = entityManager.GetBuffer<UpgradeChoice>(player);
            Check(upgrades.Length == 4, "Expected four upgrade choices");
            for (int choiceIndex = 0; choiceIndex < upgrades.Length; choiceIndex++)
            {
                for (int otherChoiceIndex = choiceIndex + 1; otherChoiceIndex < upgrades.Length; otherChoiceIndex++)
                {
                    Check(upgrades[choiceIndex].Stat != upgrades[otherChoiceIndex].Stat, "Duplicate upgrade choice");
                }
            }

            SetFirstShopOffer(3, 1);
            ApplyShopAction(ShopAction.Buy);
            ApplyShopAction(ShopAction.Reroll);
            ApplyShopAction(ShopAction.Lock);
            Check(entityManager.GetComponentData<CombatWallet>(player).Resources == money
                && !entityManager.GetBuffer<ShopOffer>(player)[0].Sold
                && !entityManager.GetBuffer<ShopOffer>(player)[0].Locked
                && entityManager.GetComponentData<PlayerProgress>(player).Result
                == ShopResult.ResolveUpgrades, "Shop accepted actions before upgrades were resolved");
            ApplyShopAction(ShopAction.Ready);
            Check(!entityManager.GetComponentData<PlayerProgress>(player).Ready, "Ready accepted with upgrades pending");
            ApplyShopAction(ShopAction.Upgrade);
            ApplyShopAction(ShopAction.Upgrade);
            ApplyShopAction(ShopAction.Ready);
            Check(entityManager.GetComponentData<PlayerProgress>(player).Ready
                && entityManager.GetComponentData<PlayerProgress>(player).PendingUpgrades == 0, "Upgrade/ready flow failed");
            ApplyShopAction(ShopAction.Reroll);
            Check(entityManager.GetComponentData<CombatWallet>(player).Resources == money, "Purchase after ready accepted");
            ApplyShopAction(ShopAction.Ready);
            Entity unknown = entityManager.CreateEntity(typeof(NetworkId));
            entityManager.SetComponentData(unknown, new NetworkId
            {
                Value = 999
            });
            version = entityManager.GetComponentData<PlayerProgress>(player).Revision;
            ApplyShopAction(ShopAction.Reroll, sourceConnection: unknown);
            Check(entityManager.GetComponentData<PlayerProgress>(player).Revision == version, "Unowned connection changed player");
            entityManager.SetComponentData(configurationEntity, new WaveProgress
            {
                Phase = WavePhase.Spawning
            });
            ApplyShopAction(ShopAction.Reroll);
            Check(entityManager.GetComponentData<PlayerProgress>(player).Revision == version, "Shop request accepted during combat");
            return "Passed: RPC ownership, replay protection, prices, locked rerolls, six slots, XP levels, upgrades and ready/phase gates.";
        }
    }
}
