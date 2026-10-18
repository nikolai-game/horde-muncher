using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher.Tests
{
    public sealed class SessionInitializationFailureDriverConstructor : INetworkStreamDriverConstructor
    {
        private readonly SessionTestUdpDriverConstructor _driver = new SessionTestUdpDriverConstructor();

        public bool FailClientInitialization { get; set; }
        public World LastClientWorld { get; private set; }

        public void CreateClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            LastClientWorld = world;
            _driver.CreateClientDriver(world, ref driverStore, netDebug);
            if (FailClientInitialization)
            {
                world.EntityManager.CreateEntity(typeof(LocalPlayerGroup));
            }
        }

        public void CreateServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            _driver.CreateServerDriver(world, ref driverStore, netDebug);
        }
    }
}
