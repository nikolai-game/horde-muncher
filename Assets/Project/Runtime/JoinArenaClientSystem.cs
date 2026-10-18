using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct JoinArenaClientSystem : ISystem
    {

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ArenaConfiguration>();
            state.EntityManager.CreateEntity(typeof(LocalPlayerGroup));
            state.EntityManager.AddComponentData(state.EntityManager.CreateEntity(), ArenaCompatibility.Current);
        }

        public void OnUpdate(ref SystemState state)
        {
            using var entityCommands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (accepted, received, entity) in
                SystemAPI.Query<RefRO<JoinArenaAcceptedRpc>, RefRO<ReceiveRpcCommandRequest>>().WithEntityAccess())
            {
                Entity connection = received.ValueRO.SourceConnection;
                if (state.EntityManager.HasComponent<NetworkId>(connection)
                    && state.EntityManager.HasComponent<NetworkStreamInGame>(connection)
                    && !state.EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection)
                    && accepted.ValueRO.LocalPlayerGroupId > 0)
                {
                    state.EntityManager.SetComponentData(SystemAPI.GetSingletonEntity<LocalPlayerGroup>(), new LocalPlayerGroup
                    {
                        Id = accepted.ValueRO.LocalPlayerGroupId,
                        JoinToken = accepted.ValueRO.JoinToken
                    });
                }

                entityCommands.DestroyEntity(entity);
            }

            foreach (var (_, connectionEntity) in
                SystemAPI.Query<RefRO<NetworkId>>().WithNone<NetworkStreamInGame, NetworkStreamRequestDisconnect>().WithEntityAccess())
            {
                entityCommands.AddComponent<NetworkStreamInGame>(connectionEntity);
                Entity requestEntity = entityCommands.CreateEntity();
                entityCommands.AddComponent(requestEntity, new JoinArenaRequest
                {
                    ProtocolVersion = SystemAPI.GetSingleton<ArenaCompatibility>().ProtocolVersion,
                    ContentVersion = SystemAPI.GetSingleton<ArenaCompatibility>().ContentVersion,
                    SharedView = SystemAPI.HasSingleton<LocalSharedView>(),
                    GroupJoinToken = SystemAPI.GetSingleton<LocalPlayerGroup>().JoinToken,
                    LocalIndex = SystemAPI.TryGetSingleton(out LocalPlayerInput input) ? input.LocalIndex : 0,
                    DisplayName = CreateDisplayName(input.LocalIndex),
                    InputName = new Unity.Collections.FixedString64Bytes(ArenaLocalInput.GetName(input))
                });
                entityCommands.AddComponent(requestEntity, new SendRpcCommandRequest
                {
                    TargetConnection = connectionEntity
                });
            }

            entityCommands.Playback(state.EntityManager);
        }

        private static FixedString64Bytes CreateDisplayName(int localIndex)
        {
            string name = System.Environment.UserName;
            name = name.Length > ArenaConnectionDefaults.MAX_DISPLAY_NAME_LENGTH ? name.Substring(0,
                ArenaConnectionDefaults.MAX_DISPLAY_NAME_LENGTH) : name;
            return new FixedString64Bytes(localIndex == 0 ? name : name + " (" + (localIndex + 1) + ")");
        }
    }
}
