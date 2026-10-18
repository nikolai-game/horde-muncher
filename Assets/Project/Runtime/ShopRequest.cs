using Unity.NetCode;

namespace HordeMuncher
{
    public struct ShopRequest : IRpcCommand
    {
        public ShopAction Action;
        public int Slot;
        public uint Revision;
    }
}
