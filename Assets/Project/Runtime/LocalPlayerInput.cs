using Unity.Entities;

namespace HordeMuncher
{
    public struct LocalPlayerInput : IComponentData
    {
        public int LocalIndex;
        public int DeviceId;
        public int KeyboardLayout;
    }
}
