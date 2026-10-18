namespace HordeMuncher.UI.Contracts
{
    public struct LobbyState
    {
        public bool IsVisible;
        public int MouseLocalIndex;
        public string Status;
        public LobbyPlayerState[] Players;
        public LobbyCharacterState[] Characters;
        public LobbyJoinHoldState[] JoinHolds;
    }
}
