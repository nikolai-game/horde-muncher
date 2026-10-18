using Unity.NetCode;

namespace HordeMuncher
{
    public struct LobbyReadyRpc : IRpcCommand
    {
        public bool Ready;
        public int CharacterId;
    }
}
