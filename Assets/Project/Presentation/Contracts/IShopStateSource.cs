namespace HordeMuncher.UI.Contracts
{
    public interface IShopStateSource
    {
        bool TryRead(out ShopState state);
    }
}
