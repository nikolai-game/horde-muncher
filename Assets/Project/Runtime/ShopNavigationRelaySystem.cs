using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ShopNavigationRelaySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (navigation, received, request) in
                SystemAPI.Query<RefRO<ShopNavigationRpc>, RefRO<ReceiveRpcCommandRequest>>().WithEntityAccess())
            {
                commands.DestroyEntity(request);
                Entity connection = received.ValueRO.SourceConnection;
                if (!EntityManager.HasComponent<NetworkId>(connection)
                    || !EntityManager.HasComponent<NetworkStreamInGame>(connection)
                    || EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection))
                {
                    continue;
                }

                int playerNumber = EntityManager.GetComponentData<NetworkId>(connection).Value;
                ShopNavigationRpc payload = navigation.ValueRO;
                foreach (var (owner, progress, wave) in
                    SystemAPI.Query<RefRO<GhostOwner>, RefRO<PlayerProgress>, RefRO<WaveStatus>>().WithAll<ArenaPlayer>())
                {
                    if (owner.ValueRO.NetworkId != playerNumber || progress.ValueRO.Revision != payload.Revision
                        || wave.ValueRO.Number != payload.WaveNumber || wave.ValueRO.Phase != payload.Phase
                        || (payload.Phase != WavePhase.Shop && payload.Phase != WavePhase.Rewards))
                    {
                        continue;
                    }

                    payload.PlayerNumber = playerNumber;
                    payload.ItemPage = System.Math.Max(0, payload.ItemPage);
                    Entity outgoing = commands.CreateEntity();
                    commands.AddComponent(outgoing, payload);
                    commands.AddComponent(outgoing, new SendRpcCommandRequest());
                    break;
                }
            }

            commands.Playback(EntityManager);
            commands.Dispose();
        }
    }
}
