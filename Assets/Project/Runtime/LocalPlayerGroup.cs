using Unity.Collections;
using Unity.Entities;

namespace HordeMuncher
{
    public struct LocalPlayerGroup : IComponentData
    {
        public int Id;
        public FixedString64Bytes JoinToken;
        public bool SharedView;
    }
}
