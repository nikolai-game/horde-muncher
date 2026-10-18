using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;

namespace HordeMuncher.Tests
{
    public sealed class SessionTestUdpDriverConstructor : INetworkStreamDriverConstructor
    {
        public void CreateClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            using NetworkSettings settings = DefaultDriverBuilder.GetNetworkClientSettings();
            DefaultDriverBuilder.RegisterClientUdpDriver(world, ref driverStore, netDebug, settings);
        }

        public void CreateServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            using NetworkSettings settings = DefaultDriverBuilder.GetNetworkServerSettings();
            DefaultDriverBuilder.RegisterServerUdpDriver(world, ref driverStore, netDebug, settings);
        }
    }
}
