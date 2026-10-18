using Unity.Collections;
using Unity.Entities;

namespace HordeMuncher
{
    public struct LobbyMember : IComponentData
    {
        public int LocalPlayerGroupId;
        public int LocalIndex;
        public int ColorIndex;
        public bool Ready;
        public bool SelectionJoined;
        public int CharacterId;
        public FixedString64Bytes DisplayName;
        public FixedString64Bytes InputName;
    }
}
