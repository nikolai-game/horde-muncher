using Unity.NetCode;

namespace HordeMuncher
{
    public struct JoinArenaRequest : IRpcCommand
    {
        public int ProtocolVersion;
        public int ContentVersion;
        public bool SharedView;
        public Unity.Collections.FixedString64Bytes GroupJoinToken;
        public int LocalIndex;
        public Unity.Collections.FixedString64Bytes DisplayName;
        public Unity.Collections.FixedString64Bytes InputName;
    }
}
