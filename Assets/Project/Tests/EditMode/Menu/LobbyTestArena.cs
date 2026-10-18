using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace HordeMuncher.Tests.Menu
{
    public sealed class LobbyTestArena : IDisposable
    {
        private readonly Dictionary<int, Entity> _primaryConnections = new Dictionary<int, Entity>();

        public World World { get; } = new World("Lobby tests");
        public EntityManager Manager => World.EntityManager;
        public Entity Lobby { get; }
        public SystemHandle JoinSystem { get; }
        public LobbyServerSystem LobbySystem { get; }

        public LobbyTestArena()
        {
            Lobby = Manager.CreateEntity(typeof(ArenaLobby));
            Entity prefab = Manager.CreateEntity(typeof(Prefab), typeof(ArenaPlayer), typeof(GhostOwner), typeof(LocalTransform),
                typeof(CombatHealth), typeof(PlayerBonuses));
            Manager.SetComponentData(prefab, new CombatHealth { Current = 100, Maximum = 100 });
            Entity configuration = Manager.CreateEntity(typeof(ArenaConfiguration));
            Manager.SetComponentData(configuration, new ArenaConfiguration { PlayerPrefab = prefab, MaxPlayers = 4 });
            JoinSystem = World.CreateSystem<JoinArenaServerSystem>();
            LobbySystem = World.CreateSystemManaged<LobbyServerSystem>();
        }

        public Entity Join(int playerNumber, int testDeviceId, int localIndex)
        {
            FixedString64Bytes token = default;
            if (localIndex > 0)
            {
                Entity primary = _primaryConnections[testDeviceId];
                if (!Manager.HasComponent<LocalPlayerGroup>(primary))
                {
                    JoinSystem.Update(World.Unmanaged);
                }

                token = Manager.GetComponentData<LocalPlayerGroup>(primary).JoinToken;
            }

            Entity connection = QueueJoin(playerNumber, localIndex, token);
            if (localIndex == 0)
            {
                _primaryConnections[testDeviceId] = connection;
            }

            return connection;
        }

        public int ReadGroupId(int testDeviceId)
        {
            return _primaryConnections.TryGetValue(testDeviceId, out Entity primary) && Manager.HasComponent<LocalPlayerGroup>(primary)
                ? Manager.GetComponentData<LocalPlayerGroup>(primary).Id : 0;
        }

        public Entity QueueJoin(int playerNumber, int localIndex, FixedString64Bytes token = default, bool sharedView = true)
        {
            Entity connection = Manager.CreateEntity(typeof(NetworkId));
            Manager.SetComponentData(connection, new NetworkId { Value = playerNumber });
            Manager.AddBuffer<LinkedEntityGroup>(connection).Add(new LinkedEntityGroup { Value = connection });
            QueueJoinRequest(connection, localIndex, token, sharedView);
            return connection;
        }

        public void QueueJoinRequest(Entity connection, int localIndex, FixedString64Bytes token = default, bool sharedView = true)
        {
            Entity request = Manager.CreateEntity(typeof(JoinArenaRequest), typeof(ReceiveRpcCommandRequest));
            Manager.SetComponentData(request, new JoinArenaRequest
            {
                ProtocolVersion = ArenaCompatibility.PROTOCOL_VERSION, ContentVersion = ArenaCompatibility.CONTENT_VERSION,
                GroupJoinToken = token, LocalIndex = localIndex, SharedView = sharedView, InputName = new FixedString64Bytes("WASD")
            });
            Manager.SetComponentData(request, new ReceiveRpcCommandRequest { SourceConnection = connection });
        }

        public void SetReady(Entity connection, bool ready, int characterId = 0, bool joinSelection = true)
        {
            if (ready && joinSelection)
            {
                UpdateSelection(connection, true, characterId);
            }

            Entity request = Manager.CreateEntity(typeof(LobbyReadyRpc), typeof(ReceiveRpcCommandRequest));
            Manager.SetComponentData(request, new LobbyReadyRpc { Ready = ready, CharacterId = characterId });
            Manager.SetComponentData(request, new ReceiveRpcCommandRequest { SourceConnection = connection });
        }

        public void UpdateSelection(Entity connection, bool joined, int characterId = 0)
        {
            Entity request = Manager.CreateEntity(typeof(LobbySelectionRpc), typeof(ReceiveRpcCommandRequest));
            Manager.SetComponentData(request, new LobbySelectionRpc { Joined = joined, CharacterId = characterId });
            Manager.SetComponentData(request, new ReceiveRpcCommandRequest { SourceConnection = connection });
        }

        public void Dispose()
        {
            World.Dispose();
        }
    }
}
