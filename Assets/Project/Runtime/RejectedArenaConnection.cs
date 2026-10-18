using Unity.Entities;

namespace HordeMuncher
{
    public struct RejectedArenaConnection : IComponentData
    {
        public double DisconnectAt;
    }
}
