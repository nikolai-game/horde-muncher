using Unity.Entities;

namespace HordeMuncher
{
    public struct ArenaEnemy : IComponentData
    {
        public float Speed;
        public float StopDistance;
        public float Radius;
        public float SeparationGap;
        public float SpawnHeight;
        public float SeparationSpeed;
        public int ContactDamage;
        public int Experience;
        public int ResourceReward;
        public float ContactCooldown;
    }
}
