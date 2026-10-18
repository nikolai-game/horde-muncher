using System.Collections.Generic;
using HordeMuncher.UI.Contracts;

namespace HordeMuncher.Tests.Shop
{
    public sealed class FakeShopModel : IShopStateSource, IShopCommands
    {
        public ShopState State = CreateState();
        public ShopNavigationState Navigation;
        public bool Available = true;
        public bool CanSend = true;
        public readonly List<(ShopCommandKind Command, int Slot, uint Revision)> Sent =
            new List<(ShopCommandKind Command, int Slot, uint Revision)>();

        public bool TryRead(out ShopState state)
        {
            state = null;
            if (!Available)
            {
                return false;
            }

            state = new ShopState
            {
                CanControl = State.CanControl,
                Navigation = State.Navigation,
                Revision = State.Revision,
                IsOpen = State.IsOpen,
                PlayerNumber = State.PlayerNumber,
                WaveNumber = State.WaveNumber,
                Resources = State.Resources,
                Level = State.Level,
                Experience = State.Experience,
                NextLevelExperience = State.NextLevelExperience,
                PendingUpgrades = State.PendingUpgrades,
                Ready = State.Ready,
                Feedback = State.Feedback,
                Stats = State.Stats,
                WeaponSlots = State.WeaponSlots,
                RerollPrice = State.RerollPrice,
                UpgradeRerollPrice = State.UpgradeRerollPrice,
                SellFraction = State.SellFraction,
                Catalog = (ShopProduct[])State.Catalog.Clone(),
                Offers = (ShopOfferState[])State.Offers.Clone(),
                Items = (ShopOwnedItem[])State.Items.Clone(),
                Weapons = (int[])State.Weapons.Clone(),
                UpgradeChoices = (ShopUpgradeChoice[])State.UpgradeChoices.Clone()
            };
            return true;
        }

        public bool TrySendNavigation(ShopNavigationState navigation)
        {
            Navigation = navigation;
            return CanSend;
        }

        public bool TrySend(ShopCommandKind command, int commandSlot, uint revision)
        {
            if (!CanSend)
            {
                return false;
            }

            Sent.Add((command, commandSlot, revision));
            return true;
        }

        public static ShopState CreateState() => new ShopState
        {
            IsOpen = true,
            Revision = 5,
            PlayerNumber = 1,
            WaveNumber = 1,
            Resources = 100,
            Level = 1,
            NextLevelExperience = 5,
            WeaponSlots = 6,
            RerollPrice = 2,
            UpgradeRerollPrice = 2,
            SellFraction = 0.5f,
            Stats = new ShopStats
            {
                Health = 100,
                MaximumHealth = 100
            },
            Catalog = new[]
            {
                new ShopProduct
                {
                    Name = "Pistol",
                    IsWeapon = true,
                    Price = 12,
                    Damage = 15,
                    Interval = 0.25f,
                    Range = 9
                },
                new ShopProduct
                {
                    Name = "Heart",
                    Description = "+20 HP",
                    Bonus = ShopBonusKind.Health,
                    Amount = 20,
                    Price = 8
                }
            },
            Offers = new[]
            {
                new ShopOfferState
                {
                    CatalogId = 0,
                    Price = 12
                },
                new ShopOfferState
                {
                    CatalogId = 1,
                    Price = 8
                }
            },
            Weapons = new[]
            {
                0
            },
            Items = new[]
            {
                new ShopOwnedItem
                {
                    CatalogId = 1,
                    Count = 2
                }
            },
            UpgradeChoices = new[]
            {
                new ShopUpgradeChoice { Stat = ShopBonusKind.Damage, Points = 5 }
            }
        };
    }
}
