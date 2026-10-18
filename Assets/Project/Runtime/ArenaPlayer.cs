using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct ArenaPlayer : IComponentData
    {
        [GhostField]
        public int ColorIndex;
        [GhostField]
        public int CharacterId;
        [GhostField]
        public int SharedViewGroup;
        [GhostField]
        public float SharedViewMaxDistance;
    }
}
