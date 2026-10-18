using Unity.Collections;
using Unity.Entities;

namespace HordeMuncher
{
    public struct ArenaConnectionFailure : IComponentData
    {
        public FixedString512Bytes Reason;
    }
}
