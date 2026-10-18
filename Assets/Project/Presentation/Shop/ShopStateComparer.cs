using System.Linq;
using HordeMuncher.UI.Contracts;

namespace HordeMuncher.UI.Shop.Presentation
{
    public static class ShopStateComparer
    {
        public static bool AreEqual(ShopState first, ShopState second)
        {
            if (first == null || second == null)
            {
                return ReferenceEquals(first, second);
            }

            return first.CanControl == second.CanControl && first.Revision == second.Revision && first.IsOpen == second.IsOpen
                && first.Navigation.Equals(second.Navigation)
                && first.IsRewardScreen == second.IsRewardScreen
                && first.PendingItemRewards.SequenceEqual(second.PendingItemRewards)
                && first.PlayerNumber == second.PlayerNumber
                && first.WaveNumber == second.WaveNumber
                && first.Resources == second.Resources
                && first.Level == second.Level
                && first.Experience == second.Experience
                && first.NextLevelExperience == second.NextLevelExperience
                && first.PendingUpgrades == second.PendingUpgrades
                && first.Ready == second.Ready
                && first.Feedback == second.Feedback
                && first.Stats.Equals(second.Stats)
                && first.WeaponSlots == second.WeaponSlots
                && first.RerollPrice == second.RerollPrice
                && first.UpgradeRerollPrice == second.UpgradeRerollPrice
                && first.SellFraction.Equals(second.SellFraction)
                && first.Catalog.SequenceEqual(second.Catalog)
                && first.Offers.SequenceEqual(second.Offers)
                && first.Items.SequenceEqual(second.Items)
                && first.Weapons.SequenceEqual(second.Weapons)
                && first.UpgradeChoices.SequenceEqual(second.UpgradeChoices);
        }
    }
}
