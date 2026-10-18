using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct ShopOffer : IBufferElementData
    {
        [GhostField]
        public int CatalogId;
        [GhostField]
        public int Price;
        [GhostField]
        public bool Locked;
        [GhostField]
        public bool Sold;
    }
}
