using System;

namespace HordeMuncher.Editor
{
    public static partial class ArenaVerification
    {
        [Serializable]
        public class ArenaWorldSnapshot
        {
            public string WorldName;
            public int ConnectionCount;
            public ArenaPlayerSnapshot[] Players;
        }
    }
}
