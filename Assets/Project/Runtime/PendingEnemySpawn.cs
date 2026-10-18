using Unity.Entities;
using Unity.Mathematics;

namespace HordeMuncher
{
    public struct PendingEnemySpawn : IComponentData
    {
        public Entity Prefab;
        public float2 Position;
        public float Remaining;
        public int WarningId;
        public bool IsBoss;
    }
}
