using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct PendingItemReward : IBufferElementData
    {
        [GhostField]
        public int CatalogId;
    }
}
