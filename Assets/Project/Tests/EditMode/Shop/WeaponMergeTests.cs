using System.Linq;
using HordeMuncher.UI.Contracts;
using HordeMuncher.UI.Shop.Presentation;
using NUnit.Framework;
using Unity.Entities;
using UnityEditor;

namespace HordeMuncher.Tests.Shop
{
    public sealed class WeaponMergeTests
    {
        [TestCase(WeaponRarity.Common)]
        [TestCase(WeaponRarity.Rare)]
        [TestCase(WeaponRarity.Epic)]
        public void FullInventoryMergesExactlyOneMatchingWeaponAndChargesOnce(WeaponRarity rarity)
        {
            using var arena = CreateArena(rarity, 6);
            EntityManager entityManager = arena.World.EntityManager;
            DynamicBuffer<CatalogEntry> catalog = entityManager.GetBuffer<CatalogEntry>(arena.Config);
            int purchasedCatalogId = entityManager.GetBuffer<ShopOffer>(arena.Player)[0].CatalogId;
            CatalogEntry purchasedWeapon = catalog[purchasedCatalogId];
            Assert.That(arena.Gateway.TryRead(out ShopState state), Is.True);
            Assert.That(ShopActionAvailability.CanExecute(state, ShopCommandKind.Buy, 0), Is.True);
            ShopViewData view = ShopViewDataBuilder.Build(state, 0, false, "");
            Assert.That(view.Offers[0].StatusText, Does.Contain("Слияние"));
            Assert.That(view.Offers[0].Caption, Does.Contain(rarity.ToString()));

            Assert.That(Buy(arena, 5), Is.EqualTo(ShopResult.Success));
            DynamicBuffer<EquippedWeapon> weapons = entityManager.GetBuffer<EquippedWeapon>(arena.Player);
            Assert.That(weapons.Length, Is.EqualTo(6));
            Assert.That(weapons[0].CatalogId, Is.EqualTo(purchasedWeapon.UpgradeCatalogId));
            Assert.That(weapons[1].CatalogId, Is.EqualTo(purchasedCatalogId));
            Assert.That(catalog[weapons[0].CatalogId].Damage, Is.GreaterThan(purchasedWeapon.Damage));
            Assert.That(entityManager.GetComponentData<CombatWallet>(arena.Player).Resources, Is.EqualTo(1000 - purchasedWeapon.Price));
            Assert.That(entityManager.GetBuffer<ShopOffer>(arena.Player)[0].Sold, Is.True);
            Assert.That(Buy(arena, 5), Is.EqualTo(ShopResult.Invalid));
            Assert.That(entityManager.GetComponentData<CombatWallet>(arena.Player).Resources, Is.EqualTo(1000 - purchasedWeapon.Price));
        }

        [TestCase(WeaponRarity.Common, 0)]
        [TestCase(WeaponRarity.Rare, 2)]
        [TestCase(WeaponRarity.Epic, 1)]
        public void InventoryMergeConsumesOneDuplicateAndUpgradesSelectedWeapon(WeaponRarity rarity, int selectedSlot)
        {
            using var arena = CreateArena(rarity, 3);
            EntityManager entityManager = arena.World.EntityManager;
            DynamicBuffer<EquippedWeapon> weapons = entityManager.GetBuffer<EquippedWeapon>(arena.Player);
            int originalCatalogId = weapons[selectedSlot].CatalogId;
            weapons[selectedSlot] = new EquippedWeapon
            {
                CatalogId = originalCatalogId,
                Cooldown = 10,
                LastTarget = arena.Player
            };
            Assert.That(arena.Gateway.TryRead(out ShopState state), Is.True);
            ShopViewData view = ShopViewDataBuilder.Build(state, 0, false, "");
            Assert.That(view.Weapons[selectedSlot].Buttons.Single(button => button.Command == ShopCommandKind.Merge).Enabled, Is.True);
            ShopCardData selectedCard = view.Weapons[selectedSlot];
            Assert.That(selectedCard.MergePartnerSlot, Is.EqualTo(selectedSlot == 0 ? 1 : 0));
            Assert.That(selectedCard.MergeResult.CatalogId, Is.EqualTo(state.Catalog[originalCatalogId].UpgradeCatalogId));
            Assert.That(selectedCard.MergeResult.Description,
                Is.EqualTo(ShopTextFormatter.FormatProductDescription(state.Catalog[selectedCard.MergeResult.CatalogId], state.Stats)));
            Assert.That(arena.Gateway.TrySend(ShopCommandKind.Merge, selectedSlot, 5), Is.True);
            using EntityQuery requests = entityManager.CreateEntityQuery(typeof(ShopRequest));
            ShopRequest request = requests.GetSingleton<ShopRequest>();
            Assert.That(request.Action, Is.EqualTo(ShopAction.Merge));
            Assert.That(request.Slot, Is.EqualTo(selectedSlot));
            DynamicBuffer<CatalogEntry> catalog = entityManager.GetBuffer<CatalogEntry>(arena.Config);
            EconomyConfig config = entityManager.GetComponentData<EconomyConfig>(arena.Config);

            Assert.That(EconomyRules.ApplyShopRequest(entityManager, arena.Player, request, config, catalog), Is.EqualTo(ShopResult.Success));

            weapons = entityManager.GetBuffer<EquippedWeapon>(arena.Player);
            int upgradedSlot = selectedSlot == 0 ? 0 : selectedSlot - 1;
            Assert.That(weapons.Length, Is.EqualTo(2));
            Assert.That(weapons[upgradedSlot].CatalogId, Is.EqualTo(catalog[originalCatalogId].UpgradeCatalogId));
            Assert.That(weapons[upgradedSlot].Cooldown, Is.Zero);
            Assert.That(weapons[upgradedSlot].LastTarget, Is.EqualTo(Entity.Null));
            Assert.That(weapons[1 - upgradedSlot].CatalogId, Is.EqualTo(originalCatalogId));
            Assert.That(entityManager.GetComponentData<CombatWallet>(arena.Player).Resources, Is.EqualTo(1000));
            Assert.That(EconomyRules.ApplyShopRequest(entityManager, arena.Player, request, config, catalog), Is.EqualTo(ShopResult.Invalid));
        }

        [TestCase(WeaponRarity.Common, 1, false)]
        [TestCase(WeaponRarity.Common, 2, true)]
        [TestCase(WeaponRarity.Legendary, 2, false)]
        public void UnavailableInventoryMergeKeepsDisabledButtonAndLeavesWeaponsIntact(
            WeaponRarity rarity, int weaponCount, bool differentWeapon)
        {
            using var arena = CreateArena(rarity, weaponCount);
            EntityManager entityManager = arena.World.EntityManager;
            if (differentWeapon)
            {
                DynamicBuffer<EquippedWeapon> differentWeapons = entityManager.GetBuffer<EquippedWeapon>(arena.Player);
                differentWeapons[1] = new EquippedWeapon { CatalogId = 1 };
            }

            Assert.That(arena.Gateway.TryRead(out ShopState state), Is.True);
            ShopViewData view = ShopViewDataBuilder.Build(state, 0, false, "");
            ShopButtonData button = view.Weapons[0].Buttons.Single(action => action.Command == ShopCommandKind.Merge);
            Assert.That(button.Text, Is.EqualTo("Объединить"));
            Assert.That(button.Enabled, Is.False);
            Assert.That(view.Weapons[0].MergeResult, Is.Null);
            Assert.That(view.Weapons[0].MergePartnerSlot, Is.EqualTo(-1));
            Assert.That(view.Weapons[0].MergeNotice, Is.Not.Empty);
            Assert.That(arena.Gateway.TrySend(ShopCommandKind.Merge, 0, 5), Is.False);
            Assert.That(EconomyRules.ApplyShopRequest(entityManager, arena.Player,
                new ShopRequest { Action = ShopAction.Merge, Slot = 0, Revision = 5 },
                entityManager.GetComponentData<EconomyConfig>(arena.Config), entityManager.GetBuffer<CatalogEntry>(arena.Config)),
                Is.EqualTo(ShopResult.Invalid));
            DynamicBuffer<EquippedWeapon> weapons = entityManager.GetBuffer<EquippedWeapon>(arena.Player);
            Assert.That(weapons.Length, Is.EqualTo(weaponCount));
            Assert.That(weapons[0].CatalogId, Is.EqualTo(state.Weapons[0]));
            Assert.That(entityManager.GetComponentData<CombatWallet>(arena.Player).Resources, Is.EqualTo(1000));
        }

        [Test]
        public void SellingMergedWeaponUsesUpgradedPrice()
        {
            using var arena = CreateArena(WeaponRarity.Common, 6);
            EntityManager entityManager = arena.World.EntityManager;
            Assert.That(Buy(arena, 5), Is.EqualTo(ShopResult.Success));
            DynamicBuffer<CatalogEntry> catalog = entityManager.GetBuffer<CatalogEntry>(arena.Config);
            EconomyConfig config = entityManager.GetComponentData<EconomyConfig>(arena.Config);
            int upgradedCatalogId = entityManager.GetBuffer<EquippedWeapon>(arena.Player)[0].CatalogId;
            int previousResources = entityManager.GetComponentData<CombatWallet>(arena.Player).Resources;
            ShopResult result = EconomyRules.ApplyShopRequest(entityManager, arena.Player,
                new ShopRequest { Action = ShopAction.Sell, Slot = 0, Revision = 6 }, config, catalog);
            Assert.That(result, Is.EqualTo(ShopResult.Success));
            Assert.That(entityManager.GetComponentData<CombatWallet>(arena.Player).Resources,
                Is.EqualTo(previousResources + EconomyRules.CalculateWeaponSellPrice(catalog[upgradedCatalogId], config)));
        }

        [TestCase(WeaponRarity.Common)]
        [TestCase(WeaponRarity.Legendary)]
        public void FreeSlotAddsWeaponWithoutMerging(WeaponRarity rarity)
        {
            using var arena = CreateArena(rarity, 1);
            EntityManager entityManager = arena.World.EntityManager;
            int purchasedCatalogId = entityManager.GetBuffer<ShopOffer>(arena.Player)[0].CatalogId;
            Assert.That(Buy(arena, 5), Is.EqualTo(ShopResult.Success));
            DynamicBuffer<EquippedWeapon> weapons = entityManager.GetBuffer<EquippedWeapon>(arena.Player);
            Assert.That(weapons.Length, Is.EqualTo(2));
            Assert.That(weapons[0].CatalogId, Is.EqualTo(purchasedCatalogId));
            Assert.That(weapons[1].CatalogId, Is.EqualTo(purchasedCatalogId));
        }

        [TestCase(WeaponRarity.Legendary, false)]
        [TestCase(WeaponRarity.Rare, false)]
        [TestCase(WeaponRarity.Common, true)]
        public void FullInventoryRejectsMaximumRarityDifferentTierOrDifferentWeapon(WeaponRarity offeredRarity, bool differentWeapon)
        {
            using var arena = CreateArena(offeredRarity, 6);
            EntityManager entityManager = arena.World.EntityManager;
            if (offeredRarity != WeaponRarity.Legendary)
            {
                DynamicBuffer<EquippedWeapon> weapons = entityManager.GetBuffer<EquippedWeapon>(arena.Player);
                for (int weaponSlot = 0; weaponSlot < weapons.Length; weaponSlot++)
                {
                    weapons[weaponSlot] = new EquippedWeapon { CatalogId = differentWeapon ? 1 : 0 };
                }
            }

            Assert.That(arena.Gateway.TryRead(out ShopState state), Is.True);
            Assert.That(ShopActionAvailability.CanExecute(state, ShopCommandKind.Buy, 0), Is.False);
            Assert.That(Buy(arena, 5), Is.EqualTo(ShopResult.FullWeapons));
            Assert.That(entityManager.GetComponentData<CombatWallet>(arena.Player).Resources, Is.EqualTo(1000));
            Assert.That(entityManager.GetBuffer<ShopOffer>(arena.Player)[0].Sold, Is.False);
        }

        [Test]
        public void UnaffordableMergeLeavesWeaponAndOfferIntact()
        {
            using var arena = CreateArena(WeaponRarity.Common, 6);
            EntityManager entityManager = arena.World.EntityManager;
            entityManager.SetComponentData(arena.Player, new CombatWallet());
            Assert.That(Buy(arena, 5), Is.EqualTo(ShopResult.NotEnoughMoney));
            Assert.That(entityManager.GetBuffer<EquippedWeapon>(arena.Player)[0].CatalogId, Is.Zero);
            Assert.That(entityManager.GetBuffer<ShopOffer>(arena.Player)[0].Sold, Is.False);
        }

        [Test]
        public void TierCatalogUsesConfiguredStatsPricesAndOriginalModels()
        {
            EconomySettings settings = AssetDatabase.LoadAssetAtPath<EconomySettings>("Assets/Project/Configs/Economy.asset");
            CatalogEntry[] catalog = CatalogDataBaking.Build(settings).ToArray();
            for (int weaponIndex = 0; weaponIndex < settings.Weapons.Length; weaponIndex++)
            {
                int catalogId = weaponIndex;
                WeaponData weapon = settings.Weapons[weaponIndex];
                WeaponTierData[] tiers = { weapon.Rare, weapon.Epic, weapon.Legendary };
                foreach (WeaponTierData tier in tiers)
                {
                    int nextCatalogId = catalog[catalogId].UpgradeCatalogId;
                    CatalogEntry upgradedWeapon = catalog[nextCatalogId];
                    Assert.That(upgradedWeapon.Price, Is.EqualTo(tier.Price));
                    Assert.That(upgradedWeapon.Damage, Is.EqualTo(tier.Damage));
                    Assert.That(upgradedWeapon.Interval, Is.EqualTo(tier.Interval));
                    Assert.That(upgradedWeapon.ExtraPierceTargets, Is.EqualTo(tier.ExtraPierceTargets));
                    Assert.That(upgradedWeapon.KnockbackDistance, Is.EqualTo(tier.KnockbackDistance));
                    Assert.That(upgradedWeapon.RetargetOnKill, Is.EqualTo(tier.RetargetOnKill));
                    Assert.That(settings.TryGetWeapon(nextCatalogId, out WeaponData modelDefinition), Is.True);
                    Assert.That(modelDefinition, Is.SameAs(weapon));
                    catalogId = nextCatalogId;
                }

                Assert.That(catalog[catalogId].Rarity, Is.EqualTo(WeaponRarity.Legendary));
                Assert.That(catalog[catalogId].UpgradeCatalogId, Is.Zero);
            }
        }

        private static ShopBridgeTestArena CreateArena(WeaponRarity rarity, int weaponCount)
        {
            var arena = new ShopBridgeTestArena();
            EntityManager entityManager = arena.World.EntityManager;
            EconomySettings settings = AssetDatabase.LoadAssetAtPath<EconomySettings>("Assets/Project/Configs/Economy.asset");
            CatalogEntry[] entries = CatalogDataBaking.Build(settings).ToArray();
            DynamicBuffer<CatalogEntry> catalog = entityManager.GetBuffer<CatalogEntry>(arena.Config);
            catalog.Clear();
            foreach (CatalogEntry entry in entries)
            {
                catalog.Add(entry);
            }

            int catalogId = 0;
            for (int tierIndex = 0; tierIndex < (int)rarity; tierIndex++)
            {
                catalogId = catalog[catalogId].UpgradeCatalogId;
            }

            DynamicBuffer<EquippedWeapon> weapons = entityManager.GetBuffer<EquippedWeapon>(arena.Player);
            weapons.Clear();
            for (int weaponSlot = 0; weaponSlot < weaponCount; weaponSlot++)
            {
                weapons.Add(new EquippedWeapon { CatalogId = catalogId });
            }

            DynamicBuffer<ShopOffer> offers = entityManager.GetBuffer<ShopOffer>(arena.Player);
            offers[0] = new ShopOffer
            {
                CatalogId = catalogId,
                Price = catalog[catalogId].Price
            };
            entityManager.SetComponentData(arena.Player, new CombatWallet { Resources = 1000 });
            return arena;
        }

        private static ShopResult Buy(ShopBridgeTestArena arena, uint revision)
        {
            EntityManager entityManager = arena.World.EntityManager;
            return EconomyRules.ApplyShopRequest(entityManager, arena.Player,
                new ShopRequest { Action = ShopAction.Buy, Slot = 0, Revision = revision },
                entityManager.GetComponentData<EconomyConfig>(arena.Config),
                entityManager.GetBuffer<CatalogEntry>(arena.Config));
        }
    }
}
