using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct JoinArenaServerSystem : ISystem
    {
        private const int REJECTION_DISCONNECT_DELAY_SECONDS = 3;
        private const int LOCAL_GROUP_SECRET_BYTES = 32;

        private Unity.Mathematics.Random _colorRandom;
        private int _lastAssignedGroupId;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ArenaConfiguration>();
            state.RequireForUpdate<JoinArenaRequest>();
            state.EntityManager.AddComponentData(state.EntityManager.CreateEntity(), ArenaCompatibility.Current);
            _colorRandom = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(1, int.MaxValue));
        }

        public void OnUpdate(ref SystemState state)
        {
            ArenaConfiguration arenaConfiguration = SystemAPI.GetSingleton<ArenaConfiguration>();
            EntityManager entityManager = state.EntityManager;
            using EntityQuery playerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ArenaPlayer>());
            int playerCount = playerQuery.CalculateEntityCount();
            int usedColors = 0;
            using NativeArray<ArenaPlayer> players = playerQuery.ToComponentDataArray<ArenaPlayer>(Allocator.Temp);
            foreach (ArenaPlayer player in players)
            {
                usedColors |= 1 << player.ColorIndex;
            }

            using var processedJoinConnections = new NativeList<Entity>(Allocator.Temp);
            var occupiedLocalSlots = new HashSet<(int GroupId, int LocalIndex)>();
            foreach (RefRO<LobbyMember> member in SystemAPI.Query<RefRO<LobbyMember>>())
            {
                occupiedLocalSlots.Add((member.ValueRO.LocalPlayerGroupId, member.ValueRO.LocalIndex));
            }

            using var entityCommands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (receivedRequest, requestEntity) in
                SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>().WithAll<JoinArenaRequest>().WithEntityAccess())
            {
                entityCommands.DestroyEntity(requestEntity);
                Entity connectionEntity = receivedRequest.ValueRO.SourceConnection;
                if (!entityManager.HasComponent<NetworkId>(connectionEntity)
                    || entityManager.HasComponent<NetworkStreamInGame>(connectionEntity)
                    || entityManager.HasComponent<NetworkStreamRequestDisconnect>(connectionEntity)
                    || entityManager.HasComponent<RejectedArenaConnection>(connectionEntity)
                    || processedJoinConnections.Contains(connectionEntity))
                {
                    continue;
                }

                processedJoinConnections.Add(connectionEntity);
                JoinArenaRequest join = entityManager.GetComponentData<JoinArenaRequest>(requestEntity);
                ArenaCompatibility compatibility = SystemAPI.GetSingleton<ArenaCompatibility>();
                if (join.ProtocolVersion != compatibility.ProtocolVersion || join.ContentVersion != compatibility.ContentVersion)
                {
                    string reason = join.ProtocolVersion != compatibility.ProtocolVersion
                        ? "Несовместимая версия игрового протокола."
                        : "Несовместимая версия игрового контента.";
                    Entity rejection = entityCommands.CreateEntity();
                    entityCommands.AddComponent(rejection, new JoinArenaRejectedRpc
                    {
                        Reason = new FixedString512Bytes(reason + " Обновите игру у клиента и хоста.")
                    });
                    entityCommands.AddComponent(rejection, new SendRpcCommandRequest { TargetConnection = connectionEntity });
                    entityCommands.AddComponent(connectionEntity, new RejectedArenaConnection
                    {
                        DisconnectAt = SystemAPI.Time.ElapsedTime + REJECTION_DISCONNECT_DELAY_SECONDS
                    });
                    continue;
                }

                bool lobbyStarted = SystemAPI.TryGetSingleton(out ArenaLobby lobby) && lobby.Started;
                if (playerCount >= math.min(ArenaLimits.MAX_PLAYERS, arenaConfiguration.MaxPlayers) || lobbyStarted
                    || !TryAuthorizeGroup(entityManager, join, out LocalPlayerGroup group)
                    || !occupiedLocalSlots.Add((group.Id, join.LocalIndex)))
                {
                    entityCommands.AddComponent(connectionEntity, new NetworkStreamRequestDisconnect
                    {
                        Reason = NetworkStreamDisconnectReason.ConnectionClose
                    });
                    continue;
                }

                if (!PlayerColorAssignment.TryAssign(usedColors, ref _colorRandom, out int colorIndex))
                {
                    entityCommands.AddComponent<NetworkStreamRequestDisconnect>(connectionEntity);
                    continue;
                }

                usedColors |= 1 << colorIndex;
                int networkId = entityManager.GetComponentData<NetworkId>(connectionEntity).Value;
                entityCommands.AddComponent(connectionEntity, new LobbyMember
                {
                    LocalPlayerGroupId = group.Id,
                    LocalIndex = join.LocalIndex,
                    ColorIndex = colorIndex,
                    DisplayName = join.DisplayName.IsEmpty ? new FixedString64Bytes("Гость " + networkId) : join.DisplayName,
                    InputName = join.InputName
                });
                entityCommands.AddComponent<NetworkStreamInGame>(connectionEntity);
                if (join.LocalIndex == 0)
                {
                    entityCommands.AddComponent(connectionEntity, group);
                }

                Entity acknowledgement = entityCommands.CreateEntity();
                entityCommands.AddComponent(acknowledgement, new JoinArenaAcceptedRpc
                {
                    LocalPlayerGroupId = group.Id,
                    JoinToken = join.LocalIndex == 0 ? group.JoinToken : default
                });
                entityCommands.AddComponent(acknowledgement, new SendRpcCommandRequest { TargetConnection = connectionEntity });
                Entity playerEntity = entityCommands.Instantiate(arenaConfiguration.PlayerPrefab);
                entityCommands.SetComponent(playerEntity, new GhostOwner
                {
                    NetworkId = networkId
                });
                entityCommands.SetComponent(playerEntity, new ArenaPlayer
                {
                    ColorIndex = colorIndex,
                    SharedViewGroup = group.SharedView ? group.Id : 0,
                    SharedViewMaxDistance = math.max(ArenaLimits.MIN_SHARED_VIEW_DISTANCE, arenaConfiguration.SharedViewMaxDistance)
                });
                entityCommands.SetComponent(playerEntity,
                    LocalTransform.FromPosition(ArenaRunRules.CalculatePlayerSpawnPosition(networkId, arenaConfiguration.MaxPlayers)));
                entityCommands.AppendToBuffer(connectionEntity, new LinkedEntityGroup
                {
                    Value = playerEntity
                });
                playerCount++;
            }

            entityCommands.Playback(entityManager);
        }

        private bool TryAuthorizeGroup(EntityManager entityManager, JoinArenaRequest join, out LocalPlayerGroup group)
        {
            group = default;
            if (join.LocalIndex < 0 || join.LocalIndex >= ArenaLimits.MAX_PLAYERS)
            {
                return false;
            }

            if (join.LocalIndex == 0)
            {
                if (!join.GroupJoinToken.IsEmpty || _lastAssignedGroupId == int.MaxValue)
                {
                    return false;
                }

                var secretBytes = new byte[LOCAL_GROUP_SECRET_BYTES];
                using RandomNumberGenerator random = RandomNumberGenerator.Create();
                random.GetBytes(secretBytes);
                group = new LocalPlayerGroup
                {
                    Id = ++_lastAssignedGroupId,
                    JoinToken = new FixedString64Bytes(Convert.ToBase64String(secretBytes)),
                    SharedView = join.SharedView
                };
                return true;
            }

            if (join.GroupJoinToken.IsEmpty)
            {
                return false;
            }

            using EntityQuery owners = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<LocalPlayerGroup>(), ComponentType.ReadOnly<LobbyMember>(),
                    ComponentType.ReadOnly<NetworkId>(), ComponentType.ReadOnly<NetworkStreamInGame>()
                },
                None = new[] { ComponentType.ReadOnly<NetworkStreamRequestDisconnect>() }
            });
            using NativeArray<LocalPlayerGroup> groups = owners.ToComponentDataArray<LocalPlayerGroup>(Allocator.Temp);
            foreach (LocalPlayerGroup candidate in groups)
            {
                if (candidate.JoinToken.Equals(join.GroupJoinToken) && candidate.SharedView == join.SharedView)
                {
                    group = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
