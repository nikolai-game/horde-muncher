using Unity.Entities;
using Unity.Mathematics;

namespace HordeMuncher
{
    public struct WaveRuntimeSettings : IComponentData
    {
        public float2 Center;
        public float2 HalfExtents;
        public float SafeDistance;
        public int MaxAlive;
        public int MaxSpawnsPerTick;
        public int PositionAttempts;
        public uint Seed;
        public float CompletionPauseDuration;
        public float EnemyHealthGrowthPerWave;
        public float EnemyDamageGrowthPerWave;
        public float EnemySpeedGrowthPerWave;
    }
}
