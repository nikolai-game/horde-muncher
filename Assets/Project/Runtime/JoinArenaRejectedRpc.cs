using Unity.Collections;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct JoinArenaRejectedRpc : IRpcCommand
    {
        public FixedString512Bytes Reason;
    }
}
