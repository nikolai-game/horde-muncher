using HordeMuncher.UI.Contracts;

namespace HordeMuncher.Tests.PlayerStatus
{
    public sealed class FakePlayerStatus : IHudStateSource, IRunResultStateSource, IRunRestartCommands
    {
        public bool Available = true;
        public bool AcceptCommands = true;
        public int CommandsSent;
        public uint SentRevision;
        public HudState Hud = new HudState
        {
            PlayerNumber = 1,
            Health = 80,
            MaximumHealth = 100,
            Level = 2,
            Experience = 3,
            NextLevelExperience = 10,
            Resources = 12,
            Kills = 4,
            WaveNumber = 3,
            WaveSecondsRemaining = 9.2f,
            Phase = RunPhase.Combat
        };
        public RunResultState Result = new RunResultState
        {
            PlayerNumber = 1,
            Revision = 7,
            Phase = RunPhase.Victory,
            ReadyPlayers = 1,
            TotalPlayers = 3
        };

        public bool TryRead(out HudState state)
        {
            state = Hud;
            return Available;
        }

        public bool TryRead(out RunResultState state)
        {
            state = Result;
            return Available;
        }

        public bool TryToggleRestartReadiness(uint revision)
        {
            if (!AcceptCommands)
            {
                return false;
            }

            CommandsSent++;
            SentRevision = revision;
            return true;
        }
    }
}
