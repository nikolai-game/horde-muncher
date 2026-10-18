using Unity.Mathematics;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct EnemySpawnWarningRpc : IRpcCommand
    {
        public int Id;
        public float2 Position;
        public float Radius;
        public bool Removed;
    }
}
