using Unity.Entities;
using Unity.Mathematics;

namespace HordeMuncher
{
    public struct ArenaProjectile : IComponentData
    {
        public Entity Shooter;
        public float3 Velocity;
        public float Radius;
        public float Remaining;
        public int Damage;
        public bool IsCritical;
        public int ExtraPierceTargets;
        public float KnockbackDistance;
        public Unity.Collections.FixedList128Bytes<Entity> HitTargets;
    }
}
