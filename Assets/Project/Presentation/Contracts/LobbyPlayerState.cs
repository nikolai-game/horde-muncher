namespace HordeMuncher.UI.Contracts
{
    public struct LobbyPlayerState
    {
        public int LocalIndex;
        public int CharacterId;
        public float ColorRed;
        public float ColorGreen;
        public float ColorBlue;
        public bool IsLocal;
        public bool IsReady;
        public bool IsInputAvailable;
        public bool SelectionJoined;
        public bool IsBackSelected;
        public string DisplayName;
    }
}
