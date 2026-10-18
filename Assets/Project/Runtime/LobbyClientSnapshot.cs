using Unity.Collections;
using Unity.Entities;

namespace HordeMuncher
{
    public struct LobbyClientSnapshot : IComponentData
    {
        public FixedString4096Bytes Players;
        public bool Started;
    }
}
