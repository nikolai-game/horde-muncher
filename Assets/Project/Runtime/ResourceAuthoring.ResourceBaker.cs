using Unity.Entities;

namespace HordeMuncher
{
    public sealed partial class ResourceAuthoring
    {
        private sealed class ResourceBaker : Baker<ResourceAuthoring>
        {
            public override void Bake(ResourceAuthoring authoring)
            {
                Entity resourceEntity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(resourceEntity, new ResourcePickup
                {
                    Value = 1
                });
                if (authoring.ItemPrefab != null)
                {
                    AddComponent(resourceEntity, new ItemDropPrefab
                    {
                        Value = GetEntity(authoring.ItemPrefab, TransformUsageFlags.Dynamic)
                    });
                }
            }
        }
    }
}
