using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct WaveStatus : IComponentData
    {
        [GhostField]
        public bool IsPaused;
        [GhostField]
        public int Number;
        [GhostField]
        public int Total;
        [GhostField]
        public WavePhase Phase;
        [GhostField(Quantization = 10)]
        public float Remaining;
        [GhostField(Quantization = 100)]
        public float CompletionElapsed;
    }
}
