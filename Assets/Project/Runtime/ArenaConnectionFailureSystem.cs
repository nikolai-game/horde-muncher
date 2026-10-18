using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(NetworkReceiveSystemGroup))]
    public partial class ArenaConnectionFailureSystem : SystemBase
    {
        protected override void OnCreate()
        {
            EntityManager.CreateEntity(typeof(ArenaConnectionFailure));
        }

        protected override void OnUpdate()
        {
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (rejected, received, entity) in
                SystemAPI.Query<RefRO<JoinArenaRejectedRpc>, RefRO<ReceiveRpcCommandRequest>>().WithEntityAccess())
            {
                Entity connection = received.ValueRO.SourceConnection;
                if (EntityManager.HasComponent<NetworkId>(connection))
                {
                    SystemAPI.SetSingleton(new ArenaConnectionFailure { Reason = rejected.ValueRO.Reason });
                    if (!EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection))
                    {
                        commands.AddComponent<NetworkStreamRequestDisconnect>(connection);
                    }
                }

                commands.DestroyEntity(entity);
            }

            if (SystemAPI.TryGetSingleton(out NetworkStreamDriver driver))
            {
                foreach (NetCodeConnectionEvent connectionEvent in driver.ConnectionEventsForTick)
                {
                    if (connectionEvent.DisconnectReason == NetworkStreamDisconnectReason.BadProtocolVersion)
                    {
                        SystemAPI.SetSingleton(new ArenaConnectionFailure
                        {
                            Reason = new FixedString512Bytes("Несовместимая версия сетевого протокола. Обновите игру у клиента и хоста.")
                        });
                    }
                }
            }

            commands.Playback(EntityManager);
        }
    }
}
