namespace HordeMuncher.UI.Contracts
{
    public interface ILobbyView
    {
        event System.Action<int> OnCharacterClicked;
        event System.Action OnLeaveLobbyClicked;
        void Render(LobbyState state);
    }
}
