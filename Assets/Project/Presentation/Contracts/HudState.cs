namespace HordeMuncher.UI.Contracts
{
    public struct HudState
    {
        public bool IsPaused;
        public bool IsLocal;
        public int PlayerNumber;
        public int Health;
        public int MaximumHealth;
        public int Level;
        public int PendingUpgrades;
        public int CollectedItems;
        public int Experience;
        public int NextLevelExperience;
        public int Resources;
        public int BankedResources;
        public int Kills;
        public int WaveNumber;
        public float WaveSecondsRemaining;
        public float WaveCompletionElapsed;
        public RunPhase Phase;
    }
}
