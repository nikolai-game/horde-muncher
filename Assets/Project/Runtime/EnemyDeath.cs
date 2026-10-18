using Unity.Entities;

namespace HordeMuncher
{
    public struct EnemyDeath : IComponentData
    {
        public Entity Killer;
    }
}
