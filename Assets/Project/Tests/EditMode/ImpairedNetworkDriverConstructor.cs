using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

namespace HordeMuncher.Tests
{
    public sealed class ImpairedNetworkDriverConstructor : INetworkStreamDriverConstructor
    {
        public void CreateClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            NetworkSettings settings = DefaultDriverBuilder.GetNetworkClientSettings();
            using NetworkSettings settingsLifetime = settings;
            settings.WithSimulatorStageParameters(maxPacketCount: 512, mode: ApplyMode.AllPackets,
                packetDelayMs: 100, packetJitterMs: 25, packetDropPercentage: 5,
                packetDuplicationPercentage: 3, randomSeed: 7391);
            var driver = new NetworkDriverStore.NetworkDriverInstance
            {
                driver = NetworkDriver.Create(new UDPNetworkInterface(), settings)
            };
            DefaultDriverBuilder.CreateClientSimulatorPipelines(ref driver);
            driverStore.RegisterDriver(TransportType.Socket, driver);
        }

        public void CreateServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            using NetworkSettings settings = DefaultDriverBuilder.GetNetworkServerSettings();
            DefaultDriverBuilder.RegisterServerUdpDriver(world, ref driverStore, netDebug, settings);
        }
    }
}
