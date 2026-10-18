using Unity.Mathematics;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct PlayerInput : IInputComponentData
    {
        public float2 Move;
        public float ViewDistance;
    }
}
