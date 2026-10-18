using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport.Relay;

namespace HordeMuncher
{
    public sealed class ArenaRelayDriverConstructor : INetworkStreamDriverConstructor
    {
        private readonly bool _isHost;
        private RelayServerData _serverData;
        private RelayServerData _clientData;

        public ArenaRelayDriverConstructor(ArenaRelayConnection connection)
        {
            _isHost = connection.IsHost;
            _serverData = connection.ServerData;
            _clientData = connection.ClientData;
        }

        public void CreateClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            if (_isHost)
            {
                DefaultDriverBuilder.RegisterClientIpcDriver(world, ref driverStore, netDebug, DefaultDriverBuilder.GetNetworkClientSettings());
                return;
            }

            DefaultDriverBuilder.RegisterClientDriver(world, ref driverStore, netDebug, ref _clientData);
        }

        public void CreateServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            DefaultDriverBuilder.RegisterServerDriver(world, ref driverStore, netDebug, ref _serverData);
        }
    }
}
