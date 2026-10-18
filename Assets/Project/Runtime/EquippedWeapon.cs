using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct EquippedWeapon : IBufferElementData
    {
        [GhostField]
        public int CatalogId;
        [GhostField]
        public uint AttackSequence;
        [GhostField(Quantization = 1000)]
        public Unity.Mathematics.float3 Aim;
        [GhostField(SendData = false)]
        public float Cooldown;
        [GhostField(SendData = false)]
        public Entity LastTarget;
    }
}
