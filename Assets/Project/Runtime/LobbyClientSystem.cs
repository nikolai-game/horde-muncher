using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateAfter(typeof(JoinArenaClientSystem))]
    public partial struct LobbyClientSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.CreateEntity(typeof(LobbyClientSnapshot));
        }

        public void OnUpdate(ref SystemState state)
        {
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (snapshot, request, entity) in
                SystemAPI.Query<RefRO<LobbySnapshotRpc>, RefRO<ReceiveRpcCommandRequest>>().WithEntityAccess())
            {
                var data = new LobbyClientSnapshot
                {
                    Players = snapshot.ValueRO.Players,
                    Started = snapshot.ValueRO.Started
                };
                if (SystemAPI.TryGetSingletonEntity<LobbyClientSnapshot>(out Entity snapshotEntity))
                {
                    state.EntityManager.SetComponentData(snapshotEntity, data);
                }
                else
                {
                    commands.AddComponent(commands.CreateEntity(), data);
                }

                commands.DestroyEntity(entity);
            }

            commands.Playback(state.EntityManager);
        }
    }
}
