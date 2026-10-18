using System;
using System.Collections.Generic;
using HordeMuncher.UI.Contracts;

namespace HordeMuncher.UI.Shop.Presentation
{
    public static class ShopViewDataBuilder
    {
        private const int MAX_UPGRADE_RARITY_INDEX = 3;

        public const int ITEMS_PER_PAGE = 12;

        public static ShopViewData Build(ShopState state, int itemPage, bool isCommandPending, string feedbackNotice,
            bool isShowingStats = false, bool usesPages = false)
        {
            bool isChoosingUpgrade = state.PendingUpgrades > 0;
            bool isRewardScreen = state.IsRewardScreen || isChoosingUpgrade || state.PendingItemRewards.Length > 0;
            int itemPageCount = usesPages ? Math.Max(1, (state.Items.Length + ITEMS_PER_PAGE - 1) / ITEMS_PER_PAGE) : 1;
            itemPage = Math.Max(0, Math.Min(itemPage, itemPageCount - 1));
            return new ShopViewData
            {
                IsVisible = state.IsOpen,
                CanControl = state.CanControl,
                PlayerNumber = state.PlayerNumber,
                IsLeveling = isChoosingUpgrade,
                IsRewardScreen = isRewardScreen,
                IsShowingStats = isShowingStats && !isRewardScreen,
                UsesPages = usesPages,
                Title = isChoosingUpgrade ? "Повышение уровня · осталось " + state.PendingUpgrades
                    : state.PendingItemRewards.Length > 0 ? "Предмет найден!"
                    : isRewardScreen ? "Награды за бой" : "Магазин · Волна " + state.WaveNumber,
                MoneyText = "● " + state.Resources,
                PlayerText = "Игрок " + state.PlayerNumber + (state.CanControl ? " · Ваш персонаж" : " · Напарник"),
                NextWaveText = isRewardScreen ? "После выбора всех наград откроется магазин"
                    : state.Ready ? "Ожидание готовности остальных игроков" : "Следующая волна: " + (state.WaveNumber + 1),
                FeedbackText = string.IsNullOrEmpty(feedbackNotice) ? ShopTextFormatter.FormatFeedback(state.Feedback) : feedbackNotice,
                ItemPage = itemPage,
                ItemPages = itemPageCount,
                WeaponCount = state.Weapons.Length,
                WeaponSlots = state.WeaponSlots,
                EmptyWeaponSlots = Math.Max(0, state.WeaponSlots - state.Weapons.Length),
                Reroll = BuildCommandButton(state,
                    isChoosingUpgrade ? "Перебросить · " + state.UpgradeRerollPrice + " ●" : "Обновить · " + state.RerollPrice + " ●",
                    "shop-reroll",
                    isChoosingUpgrade ? ShopCommandKind.RerollUpgrades : ShopCommandKind.Reroll,
                    0,
                    isCommandPending),
                Ready = BuildCommandButton(state,
                    state.Ready ? "Отменить готовность" : "В бой · Волна " + (state.WaveNumber + 1),
                    "shop-ready",
                    ShopCommandKind.Ready,
                    0,
                    isCommandPending),
                Offers = BuildOfferCards(state, isCommandPending),
                Items = BuildOwnedItemCards(state, itemPage, usesPages),
                Weapons = BuildWeaponCards(state, isCommandPending),
                Stats = BuildStatRows(state)
            };
        }

        private static ShopCardData[] BuildOfferCards(ShopState state, bool isCommandPending)
        {
            var cards = new List<ShopCardData>();
            if (state.PendingUpgrades > 0)
            {
                for (int upgradeChoiceSlot = 0; upgradeChoiceSlot < state.UpgradeChoices.Length; upgradeChoiceSlot++)
                {
                    ShopUpgradeChoice choice = state.UpgradeChoices[upgradeChoiceSlot];
                    string[] rarityStyles = { "common", "rare", "epic", "legendary" };
                    int rarity = Math.Max(0, Math.Min(MAX_UPGRADE_RARITY_INDEX, choice.Rarity));
                    var card = new ShopCardData
                    {
                        CatalogId = -1,
                        UpgradeStat = choice.Stat,
                        Name = ShopStatFormatter.GetName(choice.Stat) + " +" + choice.Points,
                        RarityStyle = "shop-rarity-" + rarityStyles[rarity],
                        Tooltip = ""
                    };
                    card.Buttons = new[]
                    {
                        BuildCommandButton(state, "Выбрать", "shop-buy", ShopCommandKind.Upgrade, upgradeChoiceSlot, isCommandPending)
                    };
                    cards.Add(card);
                }

                return cards.ToArray();
            }

            if (state.PendingItemRewards.Length > 0)
            {
                int catalogId = state.PendingItemRewards[0];
                if (ShopActionAvailability.IsCatalogIdValid(state, catalogId))
                {
                    ShopCardData card = BuildProductCard(state, catalogId);
                    int recyclePrice = Math.Max(0, (int)Math.Floor(state.Catalog[catalogId].Price * Math.Max(0, Math.Min(1, state.SellFraction))));
                    card.Buttons = new[]
                    {
                        BuildCommandButton(state, "Взять", "item-reward-take", ShopCommandKind.TakeItemReward, 0, isCommandPending),
                        BuildCommandButton(state, "Переработать (+" + recyclePrice + ")", "item-reward-recycle",
                            ShopCommandKind.RecycleItemReward, 0, isCommandPending)
                    };
                    cards.Add(card);
                }

                return cards.ToArray();
            }

            if (state.IsRewardScreen)
            {
                return cards.ToArray();
            }

            for (int offerSlot = 0; offerSlot < state.Offers.Length; offerSlot++)
            {
                ShopOfferState offer = state.Offers[offerSlot];
                if (!ShopActionAvailability.IsCatalogIdValid(state, offer.CatalogId))
                {
                    continue;
                }

                ShopCardData card = BuildProductCard(state, offer.CatalogId);
                card.IsSold = offer.Sold;
                card.IsLocked = offer.Locked;
                card.PriceText = offer.Price + " ●";
                card.IsAffordable = state.Resources >= offer.Price;
                bool isWeaponInventoryFull = card.IsWeapon && state.Weapons.Length >= state.WeaponSlots;
                bool willMerge = isWeaponInventoryFull && ShopActionAvailability.CanMergeOffer(state, offer.CatalogId);
                card.StatusText = state.Resources < offer.Price ? "Недостаточно ресурсов"
                    : willMerge ? "Слияние → " + state.Catalog[state.Catalog[offer.CatalogId].UpgradeCatalogId].Rarity
                    : isWeaponInventoryFull ? "Продайте оружие ниже" : "";
                card.Buttons = new[]
                {
                    BuildCommandButton(state,
                        (willMerge ? "Слить · " : "Купить · ") + offer.Price + " ●",
                        "shop-buy",
                        ShopCommandKind.Buy,
                        offerSlot,
                        isCommandPending),
                    BuildCommandButton(state,
                        offer.Locked ? "Открепить" : "Закрепить",
                        offer.Locked ? "shop-lock shop-locked" : "shop-lock",
                        ShopCommandKind.Lock,
                        offerSlot,
                        isCommandPending)
                };
                cards.Add(card);
            }

            return cards.ToArray();
        }

        private static ShopCardData[] BuildOwnedItemCards(ShopState state, int itemPage, bool usesPages)
        {
            var cards = new List<ShopCardData>();
            int lastItemIndexExclusive = usesPages ? Math.Min(state.Items.Length, (itemPage + 1) * ITEMS_PER_PAGE) : state.Items.Length;
            for (int ownedItemIndex = itemPage * ITEMS_PER_PAGE; ownedItemIndex < lastItemIndexExclusive; ownedItemIndex++)
            {
                ShopOwnedItem item = state.Items[ownedItemIndex];
                if (!ShopActionAvailability.IsCatalogIdValid(state, item.CatalogId))
                {
                    continue;
                }

                ShopCardData card = BuildProductCard(state, item.CatalogId);
                card.CountText = item.Count > 1 ? "×" + item.Count : "";
                card.Tooltip = card.Name + " ×" + item.Count + "\n" + ShopTextFormatter.FormatProductDescription(state.Catalog[item.CatalogId],
                    state.Stats);
                cards.Add(card);
            }

            return cards.ToArray();
        }

        private static ShopCardData[] BuildWeaponCards(ShopState state, bool isCommandPending)
        {
            var cards = new List<ShopCardData>();
            for (int weaponSlot = 0; weaponSlot < state.Weapons.Length; weaponSlot++)
            {
                int catalogId = state.Weapons[weaponSlot];
                if (!ShopActionAvailability.IsCatalogIdValid(state, catalogId))
                {
                    continue;
                }

                ShopCardData card = BuildProductCard(state, catalogId);
                int sellPrice = Math.Max(0, (int)Math.Floor(state.Catalog[catalogId].Price * Math.Max(0, Math.Min(1, state.SellFraction))));
                card.WeaponSlot = weaponSlot;
                int upgradeCatalogId = state.Catalog[catalogId].UpgradeCatalogId;
                bool hasUpgrade = upgradeCatalogId > 0 && ShopActionAvailability.IsCatalogIdValid(state, upgradeCatalogId);
                if (hasUpgrade)
                {
                    card.MergePartnerSlot = Array.FindIndex(state.Weapons,
                        candidateCatalogId => candidateCatalogId == catalogId);
                    if (card.MergePartnerSlot == weaponSlot)
                    {
                        card.MergePartnerSlot = Array.FindIndex(state.Weapons, weaponSlot + 1,
                            candidateCatalogId => candidateCatalogId == catalogId);
                    }

                    if (card.MergePartnerSlot >= 0)
                    {
                        card.MergeResult = BuildProductCard(state, upgradeCatalogId);
                        card.MergeNotice = "Слоты " + (weaponSlot + 1) + " + " + (card.MergePartnerSlot + 1)
                            + " → " + card.MergeResult.Name + " · " + state.Catalog[upgradeCatalogId].Rarity;
                    }
                    else
                    {
                        card.MergeNotice = "Нужно ещё одно такое оружие той же редкости";
                    }
                }
                else
                {
                    card.MergeNotice = "Максимальный уровень оружия";
                }

                card.Buttons = new[]
                {
                    BuildCommandButton(state, card.MergeResult != null ? "Объединить → " + state.Catalog[upgradeCatalogId].Rarity : "Объединить",
                        "shop-merge", ShopCommandKind.Merge, weaponSlot, isCommandPending),
                    BuildCommandButton(state, "Продать +" + sellPrice, "shop-sell", ShopCommandKind.Sell, weaponSlot, isCommandPending)
                };
                cards.Add(card);
            }

            return cards.ToArray();
        }

        private static ShopCardData BuildProductCard(ShopState state, int catalogId)
        {
            ShopProduct product = state.Catalog[catalogId];
            string description = ShopTextFormatter.FormatProductDescription(product, state.Stats);
            return new ShopCardData
            {
                CatalogId = catalogId,
                Name = product.Name,
                IsWeapon = product.IsWeapon,
                Caption = product.IsWeapon ? "Оружие · " + product.Rarity : "Предмет",
                RarityStyle = product.IsWeapon && !string.IsNullOrEmpty(product.Rarity)
                    ? "shop-rarity-" + product.Rarity.ToLowerInvariant() : "",
                Description = description,
                StatChanges = product.IsWeapon ? Array.Empty<ShopStatChangeData>() : BuildItemStatChanges(product),
                Tooltip = product.Name + (product.IsWeapon ? " · " + product.Rarity : "") + "\n" + description
            };
        }

        private static ShopStatChangeData[] BuildItemStatChanges(ShopProduct product)
        {
            var changes = new List<ShopStatChangeData>();
            if (product.Amount != 0)
            {
                changes.Add(BuildStatChange(product.Bonus, product.Amount));
            }

            if (product.PenaltyAmount != 0)
            {
                changes.Add(BuildStatChange(product.Penalty, product.PenaltyAmount));
            }

            return changes.ToArray();
        }

        private static ShopStatChangeData BuildStatChange(ShopBonusKind stat, float amount)
        {
            return new ShopStatChangeData
            {
                Stat = stat,
                Text = ShopTextFormatter.FormatItemStatChange(stat, amount),
                IsPenalty = amount < 0
            };
        }

        private static ShopButtonData BuildCommandButton(ShopState state,
            string text,
            string style,
            ShopCommandKind command,
            int commandSlot,
            bool isCommandPending) => new ShopButtonData
        {
            Text = text,
            Style = style,
            Command = command,
            Slot = commandSlot,
            Enabled = !isCommandPending && ShopActionAvailability.CanExecute(state, command, commandSlot)
        };

        private static ShopStatData[] BuildStatRows(ShopState state)
        {
            ShopStats stats = state.Stats;
            var rows = new List<ShopStatData>
            {
                BuildStatRow("Уровень", state.Level.ToString()),
                BuildStatRow("Опыт", state.Experience + "/" + state.NextLevelExperience)
            };
            foreach (ShopBonusKind stat in Enum.GetValues(typeof(ShopBonusKind)))
            {
                float points = ShopStatFormatter.GetPoints(stat, stats);
                ShopStatData row = BuildStatRow(ShopStatFormatter.GetName(stat),
                    stat == ShopBonusKind.Health ? stats.Health + "/" + stats.MaximumHealth : ShopStatFormatter.FormatPoints(points),
                    points != 0);
                row.Stat = stat;
                row.IsPenalty = points < 0;
                row.Tooltip = ShopStatFormatter.GetExplanation(stat, points);
                if (stat == ShopBonusKind.Regeneration)
                {
                    int healthRowIndex = rows.FindIndex(existingRow => existingRow.Stat == ShopBonusKind.Health);
                    rows.Insert(healthRowIndex + 1, row);
                }
                else
                {
                    rows.Add(row);
                }
            }

            return rows.ToArray();
        }

        private static ShopStatData BuildStatRow(string name, string displayValue, bool highlight = false) => new ShopStatData
        {
            Name = name,
            Value = displayValue,
            Highlight = highlight
        };
    }
}
