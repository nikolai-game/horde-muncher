namespace HordeMuncher.UI.Contracts
{
    public interface IShopCommands
    {
        bool TrySendNavigation(ShopNavigationState navigation);
        bool TrySend(ShopCommandKind command, int commandSlot, uint revision);
    }
}
