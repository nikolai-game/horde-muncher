using Unity.Entities;
using Unity.Mathematics;

namespace HordeMuncher
{
    public struct ResourcePickup : IComponentData
    {
        public int Value;
        public bool IsItem;
        public int ItemCatalogId;
        public bool StoreInBag;
        public Entity Collector;
        public float3 AttractionStartPosition;
        public float AttractionElapsedTime;
    }
}
