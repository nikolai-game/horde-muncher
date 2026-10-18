using Unity.Entities;

namespace HordeMuncher
{
    public sealed partial class ProjectileAuthoring
    {
        private sealed class ProjectileBaker : Baker<ProjectileAuthoring>
        {
            public override void Bake(ProjectileAuthoring authoring) => AddComponent(GetEntity(TransformUsageFlags.Dynamic), new ArenaProjectile());
        }
    }
}
