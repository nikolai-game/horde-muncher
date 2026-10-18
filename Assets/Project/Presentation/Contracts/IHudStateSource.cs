namespace HordeMuncher.UI.Contracts
{
    public interface IHudStateSource
    {
        bool TryRead(out HudState state);
    }
}
