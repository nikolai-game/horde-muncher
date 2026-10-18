using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher.Client.Shop
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ClientShopNavigationSystem : SystemBase
    {
        private readonly Dictionary<int, ShopNavigationRpc> _navigationByPlayer = new Dictionary<int, ShopNavigationRpc>();

        public bool TryRead(int playerNumber, out ShopNavigationRpc navigation)
        {
            return _navigationByPlayer.TryGetValue(playerNumber, out navigation);
        }

        protected override void OnUpdate()
        {
            var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (navigation, request) in
                SystemAPI.Query<RefRO<ShopNavigationRpc>>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
            {
                _navigationByPlayer[navigation.ValueRO.PlayerNumber] = navigation.ValueRO;
                commands.DestroyEntity(request);
            }

            commands.Playback(EntityManager);
            commands.Dispose();
        }
    }
}
