using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateAfter(typeof(JoinArenaServerSystem))]
    [UpdateBefore(typeof(RunLifecycleSystem))]
    public partial class LobbyServerSystem : SystemBase
    {
        private string _lastRoster;
        private bool _lastStarted;

        protected override void OnCreate()
        {
            RequireForUpdate<ArenaLobby>();
        }

        protected override void OnUpdate()
        {
            Entity lobbyEntity = SystemAPI.GetSingletonEntity<ArenaLobby>();
            ArenaLobby lobby = EntityManager.GetComponentData<ArenaLobby>(lobbyEntity);
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (selection, request, entity) in
                SystemAPI.Query<RefRO<LobbySelectionRpc>, RefRO<ReceiveRpcCommandRequest>>().WithEntityAccess())
            {
                Entity connection = request.ValueRO.SourceConnection;
                if (!lobby.Started && EntityManager.HasComponent<LobbyMember>(connection)
                    && !EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection)
                    && CharacterArchetypes.IsValid(selection.ValueRO.CharacterId))
                {
                    LobbyMember member = EntityManager.GetComponentData<LobbyMember>(connection);
                    if (!member.SelectionJoined || !member.Ready || !selection.ValueRO.Joined)
                    {
                        member.SelectionJoined = selection.ValueRO.Joined;
                        member.CharacterId = selection.ValueRO.CharacterId;
                        member.Ready = false;
                        EntityManager.SetComponentData(connection, member);
                    }
                }

                commands.DestroyEntity(entity);
            }

            foreach (var (ready, request, entity) in
                SystemAPI.Query<RefRO<LobbyReadyRpc>, RefRO<ReceiveRpcCommandRequest>>().WithEntityAccess())
            {
                Entity connection = request.ValueRO.SourceConnection;
                if (!lobby.Started && EntityManager.HasComponent<LobbyMember>(connection)
                    && !EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection))
                {
                    LobbyMember member = EntityManager.GetComponentData<LobbyMember>(connection);
                    if (!CharacterArchetypes.IsValid(ready.ValueRO.CharacterId)
                        || ready.ValueRO.Ready && !member.SelectionJoined)
                    {
                        commands.DestroyEntity(entity);
                        continue;
                    }

                    member.Ready = ready.ValueRO.Ready;
                    if (member.Ready)
                    {
                        member.CharacterId = ready.ValueRO.CharacterId;
                    }
                    int ownerId = EntityManager.GetComponentData<NetworkId>(connection).Value;
                    foreach (var (player, owner, playerEntity) in
                        SystemAPI.Query<RefRW<ArenaPlayer>, RefRO<GhostOwner>>().WithEntityAccess())
                    {
                        if (owner.ValueRO.NetworkId != ownerId)
                        {
                            continue;
                        }

                        player.ValueRW.CharacterId = member.CharacterId;
                        Entity prefab = SystemAPI.GetSingleton<ArenaConfiguration>().PlayerPrefab;
                        CharacterArchetypes.ApplyStartingStats(EntityManager, playerEntity, prefab, member.CharacterId);
                    }
                    EntityManager.SetComponentData(connection, member);
                }

                commands.DestroyEntity(entity);
            }

            int count = 0;
            bool allReady = true;
            string roster = "";
            foreach (var (member, networkId) in SystemAPI.Query<RefRO<LobbyMember>, RefRO<NetworkId>>()
                .WithNone<NetworkStreamRequestDisconnect>())
            {
                LobbyMember player = member.ValueRO;
                count++;
                allReady &= player.SelectionJoined && player.Ready;
                roster += networkId.ValueRO.Value + "," + player.LocalPlayerGroupId + "," + player.LocalIndex + ","
                    + (player.Ready ? "1" : "0") + "," + System.Uri.EscapeDataString(player.InputName.ToString()) + ","
                    + player.ColorIndex + "," + player.CharacterId + ","
                    + (player.SelectionJoined ? "1" : "0") + "," + System.Uri.EscapeDataString(player.DisplayName.ToString()) + ";";
            }

            if (!lobby.Started && count > 0 && allReady)
            {
                lobby.Started = true;
            }

            EntityManager.SetComponentData(lobbyEntity, lobby);
            if (roster != _lastRoster || lobby.Started != _lastStarted)
            {
                foreach (var (_, connection) in SystemAPI.Query<RefRO<LobbyMember>>()
                    .WithNone<NetworkStreamRequestDisconnect>().WithEntityAccess())
                {
                    Entity message = commands.CreateEntity();
                    commands.AddComponent(message, new LobbySnapshotRpc
                    {
                        Players = new FixedString4096Bytes(roster),
                        Started = lobby.Started
                    });
                    commands.AddComponent(message, new SendRpcCommandRequest { TargetConnection = connection });
                }

                _lastRoster = roster;
                _lastStarted = lobby.Started;
            }

            commands.Playback(EntityManager);
        }
    }
}
