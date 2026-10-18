using Unity.Collections;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct LobbySnapshotRpc : IRpcCommand
    {
        public FixedString4096Bytes Players;
        public bool Started;
    }
}
