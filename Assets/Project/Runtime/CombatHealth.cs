using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct CombatHealth : IComponentData
    {
        [GhostField]
        public int Current;
        [GhostField]
        public int Maximum;
        public float HurtCooldown;
    }
}
