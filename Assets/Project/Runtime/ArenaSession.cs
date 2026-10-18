using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HordeMuncher
{
    public sealed class ArenaSession : MonoBehaviour
    {
        private const float DISCONNECT_GRACE_SECONDS = 0.5f;
        private const int CLIENT_CONNECT_TIMEOUT_SECONDS = 20;

        private readonly List<World> _sessionWorlds = new List<World>();
        private readonly List<World> _clientWorlds = new List<World>();
        private NetworkEndpoint _endpoint;
        private readonly HashSet<World> _connectedClients = new HashSet<World>();
        private readonly Dictionary<World, float> _clientStartedAt = new Dictionary<World, float>();
        private ArenaRelayConnection _relayConnection;
        private int _connectionAttempt;
        private bool _isAddingRelayPlayer;
        private bool _hostSelectionPending;
        private readonly Dictionary<World, float> _leavingClients = new Dictionary<World, float>();
        public int LocalPlayerGroupId => TryGetLocalPlayerGroup(out LocalPlayerGroup group) ? group.Id : 0;

        public bool HasStarted
        {
            get
            {
                if (_clientWorlds.Count == 0)
                {
                    return false;
                }

                using EntityQuery query = _clientWorlds[0].EntityManager.CreateEntityQuery(typeof(LobbyClientSnapshot));
                return !query.IsEmptyIgnoreFilter && query.GetSingleton<LobbyClientSnapshot>().Started;
            }
        }
        public bool IsRunning => _sessionWorlds.Count > 0;
        public bool IsPaused
        {
            get
            {
                if (_clientWorlds.Count == 0)
                {
                    return false;
                }

                using EntityQuery players = _clientWorlds[0].EntityManager.CreateEntityQuery(typeof(ArenaPlayer), typeof(WaveStatus));
                using NativeArray<WaveStatus> statuses = players.ToComponentDataArray<WaveStatus>(Allocator.Temp);
                return statuses.Length > 0 && statuses[0].IsPaused;
            }
        }

        public IReadOnlyList<World> LocalClientWorlds => _clientWorlds;
        public ArenaMode Mode { get; private set; }
        public string RoomCode { get; private set; } = string.Empty;
        public bool HasJoinedLobby => LocalPlayerGroupId > 0 && ReadLobbyPlayers().Count > 0;
        public bool IsConnecting { get; private set; }
        public string Status { get; private set; } = "Выберите режим игры";

        private void Update()
        {
            if (!IsRunning || Mode == ArenaMode.Server)
            {
                return;
            }

            for (int clientIndex = _clientWorlds.Count - 1; clientIndex >= 0; clientIndex--)
            {
                World world = _clientWorlds[clientIndex];
                if (_leavingClients.TryGetValue(world, out float leaveStartedAt))
                {
                    if (Time.realtimeSinceStartup - leaveStartedAt >= DISCONNECT_GRACE_SECONDS)
                    {
                        RemoveSessionWorld(_clientWorlds[clientIndex]);
                    }

                    continue;
                }

                string rejectionReason = string.Empty;
                using (EntityQuery failure = world.EntityManager.CreateEntityQuery(typeof(ArenaConnectionFailure)))
                {
                    if (!failure.IsEmptyIgnoreFilter)
                    {
                        rejectionReason = failure.GetSingleton<ArenaConnectionFailure>().Reason.ToString();
                    }
                }

                if (!string.IsNullOrEmpty(rejectionReason))
                {
                    if (clientIndex == 0)
                    {
                        StopSession();
                        Status = rejectionReason;
                        return;
                    }

                    RemoveSessionWorld(_clientWorlds[clientIndex]);
                    Status = rejectionReason;
                    continue;
                }

                bool connected;
                bool disconnecting;
                using (EntityQuery connectionQuery = world.EntityManager.CreateEntityQuery(typeof(NetworkStreamInGame)))
                using (EntityQuery playerQuery = world.EntityManager.CreateEntityQuery(typeof(ArenaPlayer)))
                using (EntityQuery disconnected = world.EntityManager.CreateEntityQuery(typeof(NetworkStreamRequestDisconnect)))
                {
                    disconnecting = !disconnected.IsEmptyIgnoreFilter;
                    connected = !connectionQuery.IsEmptyIgnoreFilter && !playerQuery.IsEmptyIgnoreFilter && !disconnecting;
                }

                if (connected)
                {
                    _connectedClients.Add(world);
                    continue;
                }

                if (!_connectedClients.Contains(world) && !disconnecting
                    && Time.realtimeSinceStartup - _clientStartedAt[world] <= CLIENT_CONNECT_TIMEOUT_SECONDS)
                {
                    continue;
                }

                if (clientIndex == 0)
                {
                    bool wasConnected = _connectedClients.Contains(world);
                    StopSession();
                    Status = wasConnected ? "Соединение с хостом потеряно. Сессия завершена."
                        : "Комната недоступна: проверьте код, сеть и свободные места. Игра могла уже начаться.";
                    return;
                }

                RemoveSessionWorld(_clientWorlds[clientIndex]);
                Status = "Игрок отключён: лобби заполнено, игра началась или соединение потеряно.";
            }

            if (_hostSelectionPending && HasJoinedLobby && !HasStarted)
            {
                foreach (LobbyParticipant participant in ReadLobbyPlayers())
                {
                    if (participant.LocalPlayerGroupId != LocalPlayerGroupId || participant.LocalIndex != 0)
                    {
                        continue;
                    }

                    if (!participant.SelectionJoined)
                    {
                        LocalPlayerInput input = ReadLocalInputs().Find(candidate => candidate.LocalIndex == 0);
                        if (!ArenaLocalInput.IsAvailable(input))
                        {
                            continue;
                        }

                        SetLobbySelection(0, true);
                    }

                    _hostSelectionPending = false;
                    break;
                }
            }

            if (HasStarted)
            {
                ReadPauseInput();
                Status = Mode == ArenaMode.Local ? "Локальная игра" : Mode == ArenaMode.Host ? "Хост" : "Сетевая игра";
            }
        }

        private void OnDisable() => StopSession();

        private void OnDestroy() => StopSession();

        public async Task StartRelaySessionAsync(bool createRoom, string roomCode = "")
        {
            if (!isActiveAndEnabled || IsRunning || IsConnecting)
            {
                return;
            }

            string normalizedCode = (roomCode ?? string.Empty).Trim().ToUpperInvariant();
            if (!createRoom && (normalizedCode.Length != ArenaConnectionDefaults.RELAY_ROOM_CODE_LENGTH
                || !Array.TrueForAll(normalizedCode.ToCharArray(), character => character >= 'A' && character <= 'Z'
                || character >= '0' && character <= '9')))
            {
                Status = "Введите шестизначный код комнаты от хоста.";
                return;
            }

            int attempt = ++_connectionAttempt;
            IsConnecting = true;
            Application.runInBackground = true;
            Status = createRoom ? "Создание комнаты через Relay…" : "Подключение к комнате через Relay…";
            try
            {
                ArenaRelayConnection connection = createRoom
                    ? await ArenaRelayConnection.CreateRoomAsync()
                    : await ArenaRelayConnection.JoinRoomAsync(normalizedCode);
                if (this == null || !isActiveAndEnabled || attempt != _connectionAttempt)
                {
                    return;
                }

                _relayConnection = connection;
                RoomCode = connection.RoomCode;
                IsConnecting = false;
                StartSession(createRoom ? ArenaMode.Host : ArenaMode.Client, ArenaConnectionDefaults.LOOPBACK_ADDRESS, ArenaConnectionDefaults.PORT);
            }
            catch (Exception exception)
            {
                if (this != null && attempt == _connectionAttempt)
                {
                    StopSession();
                    Status = "Relay: не удалось подключиться. Проверьте код комнаты и интернет. " + exception.Message;
                    Debug.LogException(exception);
                }
            }
            finally
            {
                if (this != null && attempt == _connectionAttempt)
                {
                    IsConnecting = false;
                }
            }
        }

        public bool StartSession(ArenaMode mode, string address, string portText)
        {
            if (!isActiveAndEnabled || IsRunning || IsConnecting)
            {
                return false;
            }

            if (!ushort.TryParse(portText, out ushort port) || port == 0)
            {
                Status = "Порт должен быть числом от 1 до 65535";
                return false;
            }

            if (!NetworkEndpoint.TryParse(mode == ArenaMode.Client ? address.Trim() : ArenaConnectionDefaults.LOOPBACK_ADDRESS, port,
                out NetworkEndpoint endpoint))
            {
                Status = "Укажите IPv4-адрес сервера, например 127.0.0.1";
                return false;
            }

            try
            {
                Application.runInBackground = true;
                ClientServerBootstrap.AutoConnectPort = 0;
                Mode = mode;
                _endpoint = endpoint;
                if (mode != ArenaMode.Client)
                {
                    StartServer(port);
                }

                if (mode != ArenaMode.Server)
                {
                    InputDevice firstController = null;
                    foreach (InputDevice device in InputSystem.devices)
                    {
                        if ((device is Gamepad || device is Joystick) && device.added && device.enabled)
                        {
                            firstController = device;
                            break;
                        }
                    }

                    if (!TryAddLocalPlayer(firstController == null ? 1 : 0, firstController?.deviceId ?? 0))
                    {
                        throw new InvalidOperationException("Подключите клавиатуру или контроллер.");
                    }

                    _hostSelectionPending = mode == ArenaMode.Host || mode == ArenaMode.Local;
                }

                Status = mode == ArenaMode.Server ? "Сервер запущен"
                    : "Подключение к комнате…";
                return true;
            }
            catch (Exception exception)
            {
                StopSession();
                Status = "Не удалось начать игру: " + exception.Message;
                return false;
            }
        }

        public void StopSession()
        {
            ++_connectionAttempt;
            IsConnecting = false;
            _isAddingRelayPlayer = false;
            _hostSelectionPending = false;
            for (int worldIndex = _sessionWorlds.Count - 1; worldIndex >= 0; worldIndex--)
            {
                World world = _sessionWorlds[worldIndex];
                RemoveSessionWorld(world);
            }

            _sessionWorlds.Clear();
            _clientWorlds.Clear();
            _connectedClients.Clear();
            _clientStartedAt.Clear();
            _leavingClients.Clear();
            RoomCode = string.Empty;
            _relayConnection = null;
            Status = "Выберите режим игры";
        }

        public List<LobbyParticipant> ReadLobbyPlayers()
        {
            if (_clientWorlds.Count == 0)
            {
                return new List<LobbyParticipant>();
            }

            using EntityQuery query = _clientWorlds[0].EntityManager.CreateEntityQuery(typeof(LobbyClientSnapshot));
            return query.IsEmptyIgnoreFilter ? new List<LobbyParticipant>()
                : LobbyRoster.Parse(query.GetSingleton<LobbyClientSnapshot>().Players.ToString());
        }

        public List<LocalPlayerInput> ReadLocalInputs()
        {
            var inputs = new List<LocalPlayerInput>();
            foreach (World world in _clientWorlds)
            {
                if (ArenaLocalInput.TryGetBinding(world, out LocalPlayerInput input))
                {
                    inputs.Add(input);
                }
            }

            return inputs;
        }

        public bool AddLocalPlayer(int keyboardLayout, int deviceId)
        {
            if (!isActiveAndEnabled || !IsRunning || HasStarted || _clientWorlds.Count >= ArenaLimits.MAX_PLAYERS
                || ReadLobbyPlayers().Count >= ArenaLimits.MAX_PLAYERS || Mode == ArenaMode.Server)
            {
                return false;
            }

            if (!TryGetLocalPlayerGroup(out LocalPlayerGroup group) || group.JoinToken.IsEmpty)
            {
                Status = "Дождитесь подтверждения подключения первого игрока.";
                return false;
            }

            if (_relayConnection != null && !_relayConnection.IsHost)
            {
                if (_isAddingRelayPlayer)
                {
                    return false;
                }

                _ = AddRelayPlayerAsync(keyboardLayout, deviceId);
                return true;
            }

            try
            {
                return TryAddLocalPlayer(keyboardLayout, deviceId);
            }
            catch (Exception exception)
            {
                Status = "Не удалось подключить локального игрока: " + exception.Message;
                return false;
            }
        }

        public void RemoveLastLocalPlayer()
        {
            if (!HasStarted && _clientWorlds.Count > 1)
            {
                World world = _clientWorlds[_clientWorlds.Count - 1];
                using EntityQuery connections = world.EntityManager.CreateEntityQuery(typeof(NetworkId));
                if (connections.CalculateEntityCount() == 1)
                {
                    Entity connection = connections.GetSingletonEntity();
                    if (!world.EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection))
                    {
                        world.EntityManager.AddComponent<NetworkStreamRequestDisconnect>(connection);
                    }

                    _leavingClients[world] = Time.realtimeSinceStartup;
                }
            }
        }

        public void SetLobbySelection(int localIndex, bool joined, int characterId = CharacterArchetypes.BALANCED)
        {
            if (!IsRunning || HasStarted || !CharacterArchetypes.IsValid(characterId))
            {
                return;
            }

            foreach (World world in _clientWorlds)
            {
                if (!ArenaLocalInput.TryGetBinding(world, out LocalPlayerInput binding) || binding.LocalIndex != localIndex
                    || joined && !ArenaLocalInput.IsAvailable(binding))
                {
                    continue;
                }

                using EntityQuery connections = world.EntityManager.CreateEntityQuery(typeof(NetworkId), typeof(NetworkStreamInGame));
                if (connections.CalculateEntityCount() != 1)
                {
                    return;
                }

                Entity message = world.EntityManager.CreateEntity();
                world.EntityManager.AddComponentData(message, new LobbySelectionRpc { Joined = joined, CharacterId = characterId });
                world.EntityManager.AddComponentData(message, new SendRpcCommandRequest { TargetConnection = connections.GetSingletonEntity() });
            }
        }

        public void SetLobbyReady(int localIndex, bool ready, int characterId = CharacterArchetypes.BALANCED)
        {
            foreach (World world in _clientWorlds)
            {
                if (!ArenaLocalInput.TryGetBinding(world, out LocalPlayerInput binding) || binding.LocalIndex != localIndex
                    || ready && !ArenaLocalInput.IsAvailable(binding))
                {
                    continue;
                }

                using EntityQuery connections = world.EntityManager.CreateEntityQuery(typeof(NetworkId), typeof(NetworkStreamInGame));
                if (connections.CalculateEntityCount() != 1)
                {
                    return;
                }

                Entity message = world.EntityManager.CreateEntity();
                world.EntityManager.AddComponentData(message, new LobbyReadyRpc { Ready = ready, CharacterId = characterId });
                world.EntityManager.AddComponentData(message, new SendRpcCommandRequest { TargetConnection = connections.GetSingletonEntity() });
            }
        }

        public void SetPaused(bool isPaused)
        {
            if (_clientWorlds.Count == 0 || !HasStarted)
            {
                return;
            }

            EntityManager entityManager = _clientWorlds[0].EntityManager;
            using EntityQuery connections = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<NetworkStreamInGame>() },
                None = new[] { ComponentType.ReadOnly<NetworkStreamRequestDisconnect>() }
            });
            if (connections.CalculateEntityCount() != 1)
            {
                return;
            }

            Entity request = entityManager.CreateEntity(typeof(SetPauseRpc), typeof(SendRpcCommandRequest));
            entityManager.SetComponentData(request, new SetPauseRpc { IsPaused = isPaused });
            entityManager.SetComponentData(request, new SendRpcCommandRequest { TargetConnection = connections.GetSingletonEntity() });
        }

        private async Task AddRelayPlayerAsync(int keyboardLayout, int deviceId)
        {
            _isAddingRelayPlayer = true;
            int attempt = _connectionAttempt;
            try
            {
                ArenaRelayConnection connection = await ArenaRelayConnection.JoinRoomAsync(RoomCode);
                if (this != null && isActiveAndEnabled && attempt == _connectionAttempt && IsRunning && !HasStarted
                    && _clientWorlds.Count < ArenaLimits.MAX_PLAYERS)
                {
                    TryAddLocalPlayer(keyboardLayout, deviceId, connection);
                }
            }
            catch (Exception exception)
            {
                if (this != null && attempt == _connectionAttempt)
                {
                    Status = "Не удалось подключить локального игрока через Relay: " + exception.Message;
                }
            }
            finally
            {
                if (this != null && attempt == _connectionAttempt)
                {
                    _isAddingRelayPlayer = false;
                }
            }
        }

        private void StartServer(ushort port)
        {
            World serverWorld = CreateSessionWorld(true, "ArenaServer");
            serverWorld.EntityManager.CreateEntity(typeof(ArenaLobby));
            using EntityQuery connectionQuery = serverWorld.EntityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
            NetworkEndpoint listenEndpoint = Mode == ArenaMode.Local ? NetworkEndpoint.LoopbackIpv4 : NetworkEndpoint.AnyIpv4;
            RefRW<NetworkStreamDriver> serverDriver = connectionQuery.GetSingletonRW<NetworkStreamDriver>();
            if (!serverDriver.ValueRW.Listen(listenEndpoint.WithPort(_relayConnection == null ? port : (ushort)0)))
            {
                throw new InvalidOperationException("Порт занят. В другой копии игры выберите «Клиент» или измените порт.");
            }

            if (_relayConnection != null)
            {
                _endpoint = serverDriver.ValueRW.GetLocalEndPoint(serverDriver.ValueRW.DriverStore.FirstDriver);
            }

            LoadArena(serverWorld);
        }

        private World CreateSessionWorld(bool isServer, string worldName, ArenaRelayConnection relayConnection = null)
        {
            INetworkStreamDriverConstructor previousConstructor = NetworkStreamReceiveSystem.DriverConstructor;
            try
            {
                if (_relayConnection != null)
                {
                    NetworkStreamReceiveSystem.DriverConstructor = new ArenaRelayDriverConstructor(relayConnection ?? _relayConnection);
                }

                World world = isServer ? ClientServerBootstrap.CreateServerWorld(worldName) : ClientServerBootstrap.CreateClientWorld(worldName);
                _sessionWorlds.Add(world);
                return world;
            }
            finally
            {
                NetworkStreamReceiveSystem.DriverConstructor = previousConstructor;
            }
        }

        private bool TryAddLocalPlayer(int keyboardLayout, int deviceId, ArenaRelayConnection relayConnection = null)
        {
            int localIndex = 0;
            foreach (World world in _clientWorlds)
            {
                ArenaLocalInput.TryGetBinding(world, out LocalPlayerInput existing);
                if (keyboardLayout != 0 ? existing.KeyboardLayout == keyboardLayout
                    : existing.KeyboardLayout == 0 && existing.DeviceId == deviceId)
                {
                    return false;
                }

                localIndex = Mathf.Max(localIndex, existing.LocalIndex + 1);
            }

            var binding = new LocalPlayerInput
            {
                LocalIndex = localIndex, KeyboardLayout = keyboardLayout, DeviceId = deviceId
            };
            if (!ArenaLocalInput.IsAvailable(binding))
            {
                Status = "Устройство ввода недоступно";
                return false;
            }

            LocalPlayerGroup group = default;
            if (_clientWorlds.Count > 0 && (!TryGetLocalPlayerGroup(out group) || group.JoinToken.IsEmpty))
            {
                Status = "Дождитесь подтверждения подключения первого игрока.";
                return false;
            }

            World clientWorld = CreateSessionWorld(false, localIndex == 0 ? "ArenaClient" : "ArenaClient" + (localIndex + 1), relayConnection);
            try
            {
                using (EntityQuery groupQuery = clientWorld.EntityManager.CreateEntityQuery(typeof(LocalPlayerGroup)))
                {
                    clientWorld.EntityManager.SetComponentData(groupQuery.GetSingletonEntity(), group);
                }

                _clientWorlds.Add(clientWorld);
                _clientStartedAt[clientWorld] = Time.realtimeSinceStartup;
                clientWorld.EntityManager.AddComponentData(clientWorld.EntityManager.CreateEntity(), binding);
                clientWorld.EntityManager.CreateEntity(typeof(LocalSharedView));
                if (_clientWorlds.Count > 1)
                {
                    clientWorld.GetExistingSystemManaged<PresentationSystemGroup>().Enabled = false;
                }

                LoadArena(clientWorld);
                using EntityQuery connections = clientWorld.EntityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
                ArenaRelayConnection clientRelay = relayConnection ?? _relayConnection;
                NetworkEndpoint endpoint = clientRelay != null && !clientRelay.IsHost ? clientRelay.ClientData.Endpoint : _endpoint;
                connections.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(clientWorld.EntityManager, endpoint);
                return true;
            }
            catch
            {
                RemoveSessionWorld(clientWorld);
                throw;
            }
        }

        private bool TryGetLocalPlayerGroup(out LocalPlayerGroup group)
        {
            group = default;
            if (_clientWorlds.Count == 0 || !_clientWorlds[0].IsCreated)
            {
                return false;
            }

            using EntityQuery query = _clientWorlds[0].EntityManager.CreateEntityQuery(typeof(LocalPlayerGroup));
            if (query.CalculateEntityCount() != 1)
            {
                return false;
            }

            group = query.GetSingleton<LocalPlayerGroup>();
            return group.Id > 0;
        }

        private void RemoveSessionWorld(World world)
        {
            _clientWorlds.Remove(world);
            _sessionWorlds.Remove(world);
            _connectedClients.Remove(world);
            _clientStartedAt.Remove(world);
            _leavingClients.Remove(world);
            if (world.IsCreated)
            {
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(world);
                world.Dispose();
            }
        }

        private void ReadPauseInput()
        {
            if (!Application.isFocused)
            {
                return;
            }

            bool pausePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            foreach (World world in _clientWorlds)
            {
                if (ArenaLocalInput.TryGetBinding(world, out LocalPlayerInput input) && input.KeyboardLayout == 0
                    && InputSystem.GetDeviceById(input.DeviceId) is Gamepad gamepad)
                {
                    pausePressed |= gamepad.startButton.wasPressedThisFrame;
                }
            }

            if (!pausePressed || _clientWorlds.Count == 0)
            {
                return;
            }

            SetPaused(!IsPaused);
        }

        private static void LoadArena(World world)
        {
            foreach (SubScene scene in FindObjectsByType<SubScene>(FindObjectsSortMode.None))
            {
                if (scene.AutoLoadScene)
                {
                    SceneSystem.LoadSceneAsync(world.Unmanaged, scene.SceneGUID);
                }
            }
        }
    }
}
