using Unity.NetCode;

namespace HordeMuncher
{
    public struct SetPauseRpc : IRpcCommand
    {
        public bool IsPaused;
    }
}
