using Unity.NetCode;

namespace HordeMuncher
{
    public struct LobbySelectionRpc : IRpcCommand
    {
        public bool Joined;
        public int CharacterId;
    }
}
