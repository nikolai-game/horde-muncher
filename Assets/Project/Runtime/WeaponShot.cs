using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct WeaponShot : IComponentData
    {
        [GhostField]
        public uint Sequence;
        [GhostField(Quantization = 100)]
        public float3 Origin;
        [GhostField(Quantization = 100)]
        public float3 Target;
    }
}
