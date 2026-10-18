using Unity.Entities;

namespace HordeMuncher
{
    public struct WaveProgress : IComponentData
    {
        public bool IsPaused;
        public int Index;
        public float Elapsed;
        public float Budget;
        public float CompletionElapsed;
        public WavePhase Phase;
        public uint RandomState;
        public bool BossSpawned;
        public Entity Boss;
    }
}
