namespace HordeMuncher.UI.Contracts
{
    public interface IRunResultStateSource
    {
        bool TryRead(out RunResultState state);
    }
}
