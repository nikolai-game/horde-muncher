using Unity.Entities;

namespace HordeMuncher
{
    public struct WavePlan : IBufferElementData
    {
        public float Duration;
        public int SpawnRatePointOffset;
        public int SpawnRatePointCount;
        public int EnemyOffset;
        public int EnemyCount;
        public Entity BossPrefab;
    }
}
