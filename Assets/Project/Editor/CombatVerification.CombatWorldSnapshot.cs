using System;

namespace HordeMuncher.Editor
{
    public static partial class CombatVerification
    {
        [Serializable]
        public class CombatWorldSnapshot
        {
            public string WorldName;
            public int EnemyCount;
            public int ResourceCount;
            public CombatPlayerSnapshot[] Players;
        }
    }
}
