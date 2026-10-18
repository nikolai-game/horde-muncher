namespace HordeMuncher.UI.Contracts
{
    public struct RunResultState
    {
        public int PlayerNumber;
        public uint Revision;
        public RunPhase Phase;
        public bool RestartReady;
        public int ReadyPlayers;
        public int TotalPlayers;
        public bool IsFinished => Phase == RunPhase.Victory || Phase == RunPhase.Defeat;
    }
}
