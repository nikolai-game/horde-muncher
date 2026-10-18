using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct OwnedItem : IBufferElementData
    {
        [GhostField]
        public int CatalogId;
        [GhostField]
        public int Count;
    }
}
