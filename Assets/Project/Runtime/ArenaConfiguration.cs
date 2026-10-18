using Unity.Entities;

namespace HordeMuncher
{
    public struct ArenaConfiguration : IComponentData
    {
        public Entity PlayerPrefab;
        public int MaxPlayers;
        public float SharedViewMaxDistance;
        public Entity EnemyPrefab;
        public Entity ResourcePrefab;
        public Entity ProjectilePrefab;
    }
}
