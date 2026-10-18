using Unity.Entities;
using Unity.Mathematics;

namespace HordeMuncher
{
    public partial struct EnemyMovementSystem
    {
        private struct EnemyNeighbour
        {
            public Entity Entity;
            public float2 Position;
            public float Radius;
            public float SeparationGap;
        }
    }
}
