namespace HordeMuncher.UI.Contracts
{
    public interface IRunRestartCommands
    {
        bool TryToggleRestartReadiness(uint revision);
    }
}
