using System;

namespace HordeMuncher.Editor
{
    public static partial class CombatVerification
    {
        [Serializable]
        public class CombatSnapshot
        {
            public CombatWorldSnapshot[] Worlds;
        }
    }
}
