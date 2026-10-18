using System;

namespace HordeMuncher.Editor
{
    public static partial class CombatVerification
    {
        [Serializable]
        public class CombatPlayerSnapshot
        {
            public int NetworkId;
            public int Health;
            public int Resources;
            public int Kills;
            public uint ShotSequence;
        }
    }
}
