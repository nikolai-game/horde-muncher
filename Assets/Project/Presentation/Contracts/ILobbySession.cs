namespace HordeMuncher.UI.Contracts
{
    public interface ILobbySession
    {
        LobbyState ReadLobby();
        void JoinCharacterSelection(int localIndex);
        void PreviewCharacter(int localIndex, int characterId);
        void LeaveCharacterSelection(int localIndex);
        void ToggleReady(int localIndex);
        void SelectCharacter(int localIndex, int characterId);
        void LeaveLobby();
    }
}
