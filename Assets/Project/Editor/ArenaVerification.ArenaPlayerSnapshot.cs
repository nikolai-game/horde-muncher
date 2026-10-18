using System;

namespace HordeMuncher.Editor
{
    public static partial class ArenaVerification
    {
        [Serializable]
        public class ArenaPlayerSnapshot
        {
            public int NetworkId;
            public float PositionX;
            public float PositionZ;
            public bool IsLocal;
        }
    }
}
