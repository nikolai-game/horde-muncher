namespace HordeMuncher.UI.Contracts
{
    public interface ICombatNumberSource
    {
        bool TryRead(out CombatNumberState state);
        bool TryGetPanelPosition(CombatNumberState state, out float panelX, out float panelY);
    }
}
