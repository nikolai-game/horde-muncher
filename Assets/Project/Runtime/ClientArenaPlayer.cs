using Unity.Entities;

namespace HordeMuncher
{
    public readonly struct ClientArenaPlayer
    {
        public World World { get; }
        public int PlayerNumber { get; }
        public int LocalIndex { get; }

        public ClientArenaPlayer(World world, int playerNumber, int localIndex)
        {
            World = world;
            PlayerNumber = playerNumber;
            LocalIndex = localIndex;
        }
    }
}
