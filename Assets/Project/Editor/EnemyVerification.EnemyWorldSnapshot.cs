using System;

namespace HordeMuncher.Editor
{
    public static partial class EnemyVerification
    {
        [Serializable]
        public class EnemyWorldSnapshot
        {
            public string WorldName;
            public int EnemyCount;
            public int PlayerCount;
            public float MeanNearestPlayerDistance;
            public int InvalidPositionCount;
        }
    }
}
