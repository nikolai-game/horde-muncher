using Unity.Collections;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct ShopNavigationRpc : IRpcCommand
    {
        public int PlayerNumber;
        public int WaveNumber;
        public WavePhase Phase;
        public uint Revision;
        public FixedString64Bytes SelectionKey;
        public int ItemPage;
        public bool IsShowingStats;
    }
}
