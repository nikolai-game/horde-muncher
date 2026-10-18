using Unity.Collections;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct JoinArenaAcceptedRpc : IRpcCommand
    {
        public int LocalPlayerGroupId;
        public FixedString64Bytes JoinToken;
    }
}
