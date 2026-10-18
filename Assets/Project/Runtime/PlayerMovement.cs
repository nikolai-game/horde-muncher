using Unity.Entities;

namespace HordeMuncher
{
    public struct PlayerMovement : IComponentData
    {
        public float Speed;
        public float HalfExtent;
    }
}
