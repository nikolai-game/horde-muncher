using Unity.Entities;
using Unity.Mathematics;

namespace HordeMuncher
{
    public static class EconomyRules
    {
        public static int CalculatePrice(int basePrice, float priceMultiplier) =>
            (int)math.ceil(math.max(0, basePrice) * math.max(1, priceMultiplier));

        public static int CalculateShopRerollPrice(EconomyConfig config, PlayerProgress progress) =>
            CalculatePrice(config.RerollPrice + progress.Rerolls * config.RerollGrowth, progress.PriceMultiplier);

        public static int CalculateShopRerollPrice(EconomyConfig config, PlayerProgress progress, DynamicBuffer<ShopOffer> offers)
        {
            foreach (ShopOffer offer in offers)
            {
                if (!offer.Sold)
                {
                    return CalculateShopRerollPrice(config, progress);
                }
            }

            return 0;
        }

        public static int CalculateUpgradeRerollPrice(EconomyConfig config, PlayerProgress progress) =>
            CalculatePrice(math.max(1, config.UpgradeRerollPrice) + progress.UpgradeRerolls * math.max(1, config.UpgradeRerollGrowth),
                progress.PriceMultiplier);

        public static float CalculateWavePriceMultiplier(DynamicBuffer<WavePlan> waves,
            DynamicBuffer<WaveSpawnRateCurvePoint> spawnRates,
            int waveIndex,
            float enemyPriceGrowth)
        {
            if (waves.Length == 0 || waveIndex < 0 || waveIndex >= waves.Length)
            {
                return 1;
            }

            WavePlan firstWave = waves[0];
            WavePlan currentWave = waves[waveIndex];
            float firstWaveEnemies = WaveMath.CalculateSpawnAmount(spawnRates, firstWave, 0, firstWave.Duration);
            float currentWaveEnemies = WaveMath.CalculateSpawnAmount(spawnRates, currentWave, 0, currentWave.Duration);
            float enemyCountRatio = math.max(1, currentWaveEnemies / math.max(1, firstWaveEnemies));
            return 1 + math.log2(enemyCountRatio) * math.max(0, enemyPriceGrowth);
        }

        public static void AwardExperience(EntityManager entityManager, Entity playerEntity, int experienceAmount, EconomyConfig economyConfiguration)
        {
            if (experienceAmount <= 0 || !entityManager.HasComponent<PlayerProgress>(playerEntity))
            {
                return;
            }

            PlayerProgress playerProgress = entityManager.GetComponentData<PlayerProgress>(playerEntity);
            playerProgress.Xp += experienceAmount;
            playerProgress.NextLevelXp = math.max(1,
                economyConfiguration.FirstLevelXp + math.max(0, playerProgress.Level - 1) * economyConfiguration.LevelXpGrowth);
            while (playerProgress.Xp >= playerProgress.NextLevelXp)
            {
                playerProgress.Xp -= playerProgress.NextLevelXp;
                playerProgress.Level++;
                playerProgress.PendingUpgrades++;
                playerProgress.NextLevelXp = math.max(1,
                    economyConfiguration.FirstLevelXp + (playerProgress.Level - 1) * economyConfiguration.LevelXpGrowth);
            }

            entityManager.SetComponentData(playerEntity, playerProgress);
        }

        public static int RollCatalogEntry(DynamicBuffer<CatalogEntry> catalog,
            ref Random random,
            bool itemsOnly,
            int firstExcludedCatalogId = -1,
            int secondExcludedCatalogId = -1)
        {
            float totalWeight = 0;
            for (int catalogIndex = 0; catalogIndex < catalog.Length; catalogIndex++)
            {
                if ((!itemsOnly || catalog[catalogIndex].Kind == GoodsKind.Item) && catalogIndex != firstExcludedCatalogId
                    && catalogIndex != secondExcludedCatalogId)
                {
                    totalWeight += catalog[catalogIndex].Weight;
                }
            }

            if (totalWeight <= 0)
            {
                return -1;
            }

            float remainingWeight = random.NextFloat(0, totalWeight);
            for (int catalogIndex = 0; catalogIndex < catalog.Length; catalogIndex++)
            {
                if ((itemsOnly && catalog[catalogIndex].Kind != GoodsKind.Item) || catalogIndex == firstExcludedCatalogId
                    || catalogIndex == secondExcludedCatalogId)
                {
                    continue;
                }

                remainingWeight -= catalog[catalogIndex].Weight;
                if (remainingWeight <= 0)
                {
                    return catalogIndex;
                }
            }

            return -1;
        }

        public static void RefreshShopOffers(EntityManager entityManager,
            Entity playerEntity,
            DynamicBuffer<CatalogEntry> catalog,
            ref PlayerProgress playerProgress)
        {
            var random = new Random(playerProgress.RandomState == 0 ? 1u : playerProgress.RandomState);
            DynamicBuffer<ShopOffer> offers = entityManager.GetBuffer<ShopOffer>(playerEntity);
            while (offers.Length < 4)
            {
                offers.Add(new ShopOffer
                {
                    CatalogId = -1
                });
            }

            for (int offerSlot = 0; offerSlot < offers.Length; offerSlot++)
            {
                if (offers[offerSlot].Locked && !offers[offerSlot].Sold)
                {
                    ShopOffer lockedOffer = offers[offerSlot];
                    if (lockedOffer.CatalogId >= 0 && lockedOffer.CatalogId < catalog.Length)
                    {
                        lockedOffer.Price = CalculatePrice(catalog[lockedOffer.CatalogId].Price, playerProgress.PriceMultiplier);
                        offers[offerSlot] = lockedOffer;
                    }

                    continue;
                }

                int catalogId = RollCatalogEntry(catalog, ref random, false);
                offers[offerSlot] = new ShopOffer
                {
                    CatalogId = catalogId,
                    Price = catalogId >= 0 ? CalculatePrice(catalog[catalogId].Price, playerProgress.PriceMultiplier) : 0,
                    Sold = catalogId < 0
                };
            }

            playerProgress.RandomState = random.state;
        }

        public static void GenerateUpgradeChoices(EntityManager entityManager,
            Entity playerEntity,
            ref PlayerProgress playerProgress)
        {
            DynamicBuffer<UpgradeChoice> choices = entityManager.GetBuffer<UpgradeChoice>(playerEntity);
            choices.Clear();
            if (playerProgress.PendingUpgrades <= 0)
            {
                return;
            }

            var random = new Random(playerProgress.RandomState == 0 ? 1u : playerProgress.RandomState);
            PlayerBonuses bonuses = entityManager.GetComponentData<PlayerBonuses>(playerEntity);
            var candidates = new Unity.Collections.FixedList128Bytes<BonusKind>();
            for (int statIndex = 0; statIndex <= (int)BonusKind.Luck; statIndex++)
            {
                BonusKind stat = (BonusKind)statIndex;
                if (stat != BonusKind.CriticalChance || bonuses.CriticalChance < 1)
                {
                    candidates.Add(stat);
                }
            }

            for (int choiceSlot = 0; choiceSlot < 4; choiceSlot++)
            {
                int candidateIndex = random.NextInt(candidates.Length);
                BonusKind stat = candidates[candidateIndex];
                candidates.RemoveAt(candidateIndex);
                StatUpgradeRarity rarity = StatUpgradeRules.RollRarity(bonuses.Luck, ref random);
                choices.Add(new UpgradeChoice
                {
                    Stat = stat,
                    Rarity = rarity,
                    Points = StatUpgradeRules.GetBasePoints(stat) * ((int)rarity + 1)
                });
            }

            playerProgress.RandomState = random.state;
        }

        public static void ApplyItemBonus(EntityManager entityManager, Entity playerEntity, int catalogId, CatalogEntry item)
        {
            PlayerBonuses playerBonuses = entityManager.GetComponentData<PlayerBonuses>(playerEntity);
            ApplyStatBonus(entityManager, playerEntity, ref playerBonuses, item.Bonus, item.Amount);
            ApplyStatBonus(entityManager, playerEntity, ref playerBonuses, item.Penalty, math.min(0, item.PenaltyAmount));
            entityManager.SetComponentData(playerEntity, playerBonuses);
            DynamicBuffer<OwnedItem> inventory = entityManager.GetBuffer<OwnedItem>(playerEntity);
            for (int ownedItemIndex = 0; ownedItemIndex < inventory.Length; ownedItemIndex++)
            {
                if (inventory[ownedItemIndex].CatalogId == catalogId)
                {
                    OwnedItem ownedItem = inventory[ownedItemIndex];
                    ownedItem.Count++;
                    inventory[ownedItemIndex] = ownedItem;
                    return;
                }
            }

            inventory.Add(new OwnedItem
            {
                CatalogId = catalogId,
                Count = 1
            });
        }

        public static int CalculateWeaponSellPrice(CatalogEntry goods,
            EconomyConfig economyConfiguration) => math.max(0,
            (int)math.floor(goods.Price * math.clamp(economyConfiguration.WeaponSellFraction, 0, 1)));

        public static int CalculateItemRecyclePrice(CatalogEntry item, EconomyConfig economyConfiguration) =>
            math.max(0, (int)math.floor(item.Price * math.clamp(economyConfiguration.WeaponSellFraction, 0, 1)));

        public static ShopResult ApplyShopRequest(EntityManager entityManager,
            Entity playerEntity,
            ShopRequest request,
            EconomyConfig economyConfiguration,
            DynamicBuffer<CatalogEntry> catalog)
        {
            PlayerProgress playerProgress = entityManager.GetComponentData<PlayerProgress>(playerEntity);
            if (request.Revision != playerProgress.Revision)
            {
                return ShopResult.Invalid;
            }

            playerProgress.Revision++;
            ShopResult result = ShopResult.Invalid;
            CombatWallet wallet = entityManager.GetComponentData<CombatWallet>(playerEntity);
            DynamicBuffer<ShopOffer> offers = entityManager.GetBuffer<ShopOffer>(playerEntity);
            bool hasItemRewards = entityManager.HasBuffer<PendingItemReward>(playerEntity)
                && entityManager.GetBuffer<PendingItemReward>(playerEntity).Length > 0;
            if (request.Action == ShopAction.Ready)
            {
                if (playerProgress.PendingUpgrades > 0 || hasItemRewards)
                {
                    result = ShopResult.ResolveUpgrades;
                }
                else
                {
                    playerProgress.Ready = !playerProgress.Ready;
                    result = ShopResult.Success;
                }
            }
            else if (playerProgress.PendingUpgrades > 0 && request.Action != ShopAction.Upgrade
                && request.Action != ShopAction.RerollUpgrades)
            {
                result = ShopResult.ResolveUpgrades;
            }
            else if (hasItemRewards && request.Action != ShopAction.TakeItemReward && request.Action != ShopAction.RecycleItemReward
                && request.Action != ShopAction.Upgrade && request.Action != ShopAction.RerollUpgrades)
            {
                result = ShopResult.ResolveUpgrades;
            }
            else if (playerProgress.Ready)
            {
                result = ShopResult.AlreadyReady;
            }
            else
            {
                switch (request.Action)
                {
                    case ShopAction.TakeItemReward:
                    case ShopAction.RecycleItemReward:
                        if (!hasItemRewards)
                        {
                            break;
                        }

                        DynamicBuffer<PendingItemReward> rewards = entityManager.GetBuffer<PendingItemReward>(playerEntity);
                        int rewardCatalogId = rewards[0].CatalogId;
                        if (rewardCatalogId < 0 || rewardCatalogId >= catalog.Length || catalog[rewardCatalogId].Kind != GoodsKind.Item)
                        {
                            break;
                        }

                        if (request.Action == ShopAction.TakeItemReward)
                        {
                            ApplyItemBonus(entityManager, playerEntity, rewardCatalogId, catalog[rewardCatalogId]);
                        }
                        else
                        {
                            wallet.Resources += CalculateItemRecyclePrice(catalog[rewardCatalogId], economyConfiguration);
                        }

                        rewards.RemoveAt(0);
                        playerProgress.CollectedItems = math.max(0, playerProgress.CollectedItems - 1);
                        result = ShopResult.Success;
                        break;
                    case ShopAction.Buy:
                        if (request.Slot < 0 || request.Slot >= offers.Length)
                        {
                            break;
                        }

                        ShopOffer offer = offers[request.Slot];
                        if (offer.Sold || offer.CatalogId < 0 || offer.CatalogId >= catalog.Length)
                        {
                            break;
                        }

                        CatalogEntry goods = catalog[offer.CatalogId];
                        if (wallet.Resources < offer.Price)
                        {
                            result = ShopResult.NotEnoughMoney;
                            break;
                        }

                        if (goods.Kind == GoodsKind.Weapon)
                        {
                            DynamicBuffer<EquippedWeapon> weapons = entityManager.GetBuffer<EquippedWeapon>(playerEntity);
                            if (weapons.Length >= economyConfiguration.WeaponSlots)
                            {
                                int mergeWeaponSlot = -1;
                                if (goods.UpgradeCatalogId > 0 && goods.UpgradeCatalogId < catalog.Length)
                                {
                                    for (int weaponSlot = 0; weaponSlot < weapons.Length; weaponSlot++)
                                    {
                                        if (weapons[weaponSlot].CatalogId == offer.CatalogId)
                                        {
                                            mergeWeaponSlot = weaponSlot;
                                            break;
                                        }
                                    }
                                }

                                if (mergeWeaponSlot < 0)
                                {
                                    result = ShopResult.FullWeapons;
                                    break;
                                }

                                EquippedWeapon mergedWeapon = weapons[mergeWeaponSlot];
                                mergedWeapon.CatalogId = goods.UpgradeCatalogId;
                                mergedWeapon.Cooldown = 0;
                                mergedWeapon.LastTarget = Entity.Null;
                                weapons[mergeWeaponSlot] = mergedWeapon;
                            }
                            else
                            {
                                weapons.Add(new EquippedWeapon
                                {
                                    CatalogId = offer.CatalogId
                                });
                            }
                        }
                        else
                        {
                            ApplyItemBonus(entityManager, playerEntity, offer.CatalogId, goods);
                        }

                        wallet.Resources -= offer.Price;
                        offer.Sold = true;
                        offer.Locked = false;
                        offers[request.Slot] = offer;
                        result = ShopResult.Success;
                        break;
                    case ShopAction.Merge:
                        DynamicBuffer<EquippedWeapon> inventoryWeapons = entityManager.GetBuffer<EquippedWeapon>(playerEntity);
                        if (request.Slot < 0 || request.Slot >= inventoryWeapons.Length)
                        {
                            break;
                        }

                        EquippedWeapon selectedWeapon = inventoryWeapons[request.Slot];
                        int selectedCatalogId = selectedWeapon.CatalogId;
                        if (selectedCatalogId < 0 || selectedCatalogId >= catalog.Length
                            || catalog[selectedCatalogId].Kind != GoodsKind.Weapon)
                        {
                            break;
                        }

                        int upgradeCatalogId = catalog[selectedCatalogId].UpgradeCatalogId;
                        if (upgradeCatalogId <= 0 || upgradeCatalogId >= catalog.Length)
                        {
                            break;
                        }

                        int matchingWeaponSlot = -1;
                        for (int weaponSlot = 0; weaponSlot < inventoryWeapons.Length; weaponSlot++)
                        {
                            if (weaponSlot != request.Slot && inventoryWeapons[weaponSlot].CatalogId == selectedCatalogId)
                            {
                                matchingWeaponSlot = weaponSlot;
                                break;
                            }
                        }

                        if (matchingWeaponSlot < 0)
                        {
                            break;
                        }

                        selectedWeapon.CatalogId = upgradeCatalogId;
                        selectedWeapon.Cooldown = 0;
                        selectedWeapon.LastTarget = Entity.Null;
                        inventoryWeapons[request.Slot] = selectedWeapon;
                        inventoryWeapons.RemoveAt(matchingWeaponSlot);
                        result = ShopResult.Success;
                        break;
                    case ShopAction.Sell:
                        DynamicBuffer<EquippedWeapon> ownedWeapons = entityManager.GetBuffer<EquippedWeapon>(playerEntity);
                        if (request.Slot < 0 || request.Slot >= ownedWeapons.Length)
                        {
                            break;
                        }

                        if (ownedWeapons.Length <= 1)
                        {
                            result = ShopResult.LastWeapon;
                            break;
                        }

                        int soldCatalogId = ownedWeapons[request.Slot].CatalogId;
                        if (soldCatalogId < 0 || soldCatalogId >= catalog.Length || catalog[soldCatalogId].Kind != GoodsKind.Weapon)
                        {
                            break;
                        }

                        wallet.Resources += CalculateWeaponSellPrice(catalog[soldCatalogId], economyConfiguration);
                        ownedWeapons.RemoveAt(request.Slot);
                        result = ShopResult.Success;
                        break;
                    case ShopAction.Reroll:
                        int rerollPrice = CalculateShopRerollPrice(economyConfiguration, playerProgress, offers);
                        if (wallet.Resources < rerollPrice)
                        {
                            result = ShopResult.NotEnoughMoney;
                            break;
                        }

                        bool canRoll = false;
                        foreach (ShopOffer shopOffer in offers)
                        {
                            if (!shopOffer.Locked || shopOffer.Sold)
                            {
                                canRoll = true;
                            }
                        }

                        if (!canRoll)
                        {
                            break;
                        }

                        wallet.Resources -= rerollPrice;
                        if (rerollPrice > 0)
                        {
                            playerProgress.Rerolls++;
                        }
                        RefreshShopOffers(entityManager, playerEntity, catalog, ref playerProgress);
                        result = ShopResult.Success;
                        break;
                    case ShopAction.Lock:
                        if (request.Slot < 0 || request.Slot >= offers.Length || offers[request.Slot].Sold)
                        {
                            break;
                        }

                        ShopOffer lockedOffer = offers[request.Slot];
                        lockedOffer.Locked = !lockedOffer.Locked;
                        offers[request.Slot] = lockedOffer;
                        result = ShopResult.Success;
                        break;
                    case ShopAction.RerollUpgrades:
                        if (playerProgress.PendingUpgrades <= 0)
                        {
                            break;
                        }

                        int upgradeRerollPrice = CalculateUpgradeRerollPrice(economyConfiguration, playerProgress);
                        if (wallet.Resources < upgradeRerollPrice)
                        {
                            result = ShopResult.NotEnoughMoney;
                            break;
                        }

                        wallet.Resources -= upgradeRerollPrice;
                        playerProgress.UpgradeRerolls++;
                        GenerateUpgradeChoices(entityManager, playerEntity, ref playerProgress);
                        result = ShopResult.Success;
                        break;
                    case ShopAction.Upgrade:
                        DynamicBuffer<UpgradeChoice> choices = entityManager.GetBuffer<UpgradeChoice>(playerEntity);
                        if (playerProgress.PendingUpgrades <= 0 || request.Slot < 0 || request.Slot >= choices.Length)
                        {
                            break;
                        }

                        UpgradeChoice choice = choices[request.Slot];
                        PlayerBonuses bonuses = entityManager.GetComponentData<PlayerBonuses>(playerEntity);
                        ApplyStatBonus(entityManager, playerEntity, ref bonuses, choice.Stat,
                            StatUpgradeRules.ToBonusAmount(choice.Stat, choice.Points));
                        entityManager.SetComponentData(playerEntity, bonuses);
                        playerProgress.PendingUpgrades--;
                        GenerateUpgradeChoices(entityManager, playerEntity, ref playerProgress);
                        result = ShopResult.Success;
                        break;
                }
            }

            playerProgress.Result = result;
            entityManager.SetComponentData(playerEntity, wallet);
            entityManager.SetComponentData(playerEntity, playerProgress);
            return result;
        }

        private static void ApplyStatBonus(EntityManager entityManager,
            Entity playerEntity,
            ref PlayerBonuses playerBonuses,
            BonusKind bonus,
            float amount)
        {
            if (amount == 0)
            {
                return;
            }

            switch (bonus)
            {
                case BonusKind.Health:
                    CombatHealth health = entityManager.GetComponentData<CombatHealth>(playerEntity);
                    int healthIncrease = (int)math.round(amount);
                    int previousMaximum = health.Maximum;
                    health.Maximum = math.max(1, health.Maximum + healthIncrease);
                    if (health.Current > 0)
                    {
                        health.Current = math.clamp(health.Current + health.Maximum - previousMaximum, 1, health.Maximum);
                    }

                    entityManager.SetComponentData(playerEntity, health);
                    break;
                case BonusKind.Luck:
                    playerBonuses.Luck += (int)math.round(amount);
                    break;
                case BonusKind.Damage:
                    playerBonuses.Damage += amount;
                    break;
                case BonusKind.MeleeDamage:
                    playerBonuses.MeleeDamage += amount;
                    break;
                case BonusKind.RangedDamage:
                    playerBonuses.RangedDamage += amount;
                    break;
                case BonusKind.AttackSpeed:
                    playerBonuses.AttackSpeed += amount;
                    break;
                case BonusKind.MoveSpeed:
                    playerBonuses.MoveSpeed += amount;
                    break;
                case BonusKind.Range:
                    playerBonuses.Range += amount;
                    break;
                case BonusKind.Pickup:
                    playerBonuses.Pickup += amount;
                    break;
                case BonusKind.Armor:
                    playerBonuses.Armor += (int)math.round(amount);
                    break;
                case BonusKind.Regeneration:
                    playerBonuses.Regeneration += amount;
                    break;
                case BonusKind.CriticalChance:
                    playerBonuses.CriticalChance = math.saturate(playerBonuses.CriticalChance + amount);
                    break;
                case BonusKind.CriticalDamageMultiplier:
                    playerBonuses.CriticalDamageMultiplier = math.max(1, playerBonuses.CriticalDamageMultiplier + amount);
                    break;
            }
        }
    }
}
