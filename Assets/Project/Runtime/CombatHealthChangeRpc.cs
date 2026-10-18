using Unity.Mathematics;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct CombatHealthChangeRpc : IRpcCommand
    {
        public float3 Position;
        public int HealthChange;
        public bool IsPlayer;
        public bool IsCritical;
    }
}
