using System;

namespace HordeMuncher.Editor
{
    public static partial class ArenaVerification
    {
        [Serializable]
        public class ArenaSnapshot
        {
            public ArenaWorldSnapshot[] Worlds;
        }
    }
}
