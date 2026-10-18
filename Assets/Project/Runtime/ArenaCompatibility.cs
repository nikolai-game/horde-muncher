using Unity.Entities;

namespace HordeMuncher
{
    public struct ArenaCompatibility : IComponentData
    {
        public const int PROTOCOL_VERSION = 1;
        public const int CONTENT_VERSION = 1;

        public int ProtocolVersion;
        public int ContentVersion;

        public static ArenaCompatibility Current => new ArenaCompatibility
        {
            ProtocolVersion = PROTOCOL_VERSION,
            ContentVersion = CONTENT_VERSION
        };
    }
}
