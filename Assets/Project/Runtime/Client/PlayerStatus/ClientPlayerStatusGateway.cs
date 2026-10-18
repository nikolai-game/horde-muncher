using System;
using HordeMuncher.UI.Contracts;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher.Client.PlayerStatus
{
    public sealed class ClientPlayerStatusGateway : IHudStateSource, IRunResultStateSource, IRunRestartCommands, IDisposable
    {
        private readonly World _world;
        private readonly int _playerNumber;
        private readonly EntityQuery _players;
        private readonly EntityQuery _allPlayers;
        private readonly EntityQuery _connections;
        private bool _disposed;

        public ClientPlayerStatusGateway(World world, int playerNumber = 0)
        {
            if (world == null || !world.IsCreated || !world.IsClient())
            {
                throw new ArgumentException("A live client world is required.", nameof(world));
            }

            _world = world;
            _playerNumber = playerNumber;
            EntityManager entityManager = world.EntityManager;
            _players = entityManager.CreateEntityQuery(typeof(ArenaPlayer),
                typeof(GhostOwner),
                typeof(CombatHealth),
                typeof(CombatWallet),
                typeof(PlayerProgress),
                typeof(WaveStatus));
            _allPlayers = entityManager.CreateEntityQuery(typeof(ArenaPlayer), typeof(PlayerProgress));
            _connections = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<NetworkStreamInGame>(),
                    ComponentType.ReadOnly<NetworkId>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<NetworkStreamRequestDisconnect>()
                }
            });
        }

        public bool TryRead(out HudState state)
        {
            state = default;
            if (!TryGetPlayer(out Entity player, out _))
            {
                return false;
            }

            EntityManager entityManager = _world.EntityManager;
            CombatHealth health = entityManager.GetComponentData<CombatHealth>(player);
            CombatWallet wallet = entityManager.GetComponentData<CombatWallet>(player);
            PlayerProgress progress = entityManager.GetComponentData<PlayerProgress>(player);
            WaveStatus wave = entityManager.GetComponentData<WaveStatus>(player);
            state = new HudState
            {
                IsPaused = wave.IsPaused,
                IsLocal = IsOwnedPlayer(player),
                PlayerNumber = entityManager.GetComponentData<GhostOwner>(player).NetworkId,
                Health = health.Current,
                MaximumHealth = health.Maximum,
                Resources = wallet.Resources,
                BankedResources = wallet.BankedResources,
                Kills = wallet.Kills,
                Level = progress.Level,
                PendingUpgrades = progress.PendingUpgrades,
                CollectedItems = progress.CollectedItems,
                Experience = progress.Xp,
                NextLevelExperience = progress.NextLevelXp,
                WaveNumber = wave.Number,
                WaveSecondsRemaining = wave.Remaining,
                WaveCompletionElapsed = wave.CompletionElapsed,
                Phase = MapPhase(wave.Phase)
            };
            return true;
        }

        public bool TryRead(out RunResultState state)
        {
            state = default;
            if (!TryGetPlayer(out Entity player, out _))
            {
                return false;
            }

            EntityManager entityManager = _world.EntityManager;
            PlayerProgress progress = entityManager.GetComponentData<PlayerProgress>(player);
            state = new RunResultState
            {
                PlayerNumber = entityManager.GetComponentData<GhostOwner>(player).NetworkId,
                Revision = progress.Revision,
                RestartReady = progress.RestartReady,
                Phase = MapPhase(entityManager.GetComponentData<WaveStatus>(player).Phase)
            };
            using NativeArray<PlayerProgress> players = _allPlayers.ToComponentDataArray<PlayerProgress>(Allocator.Temp);
            state.TotalPlayers = players.Length;
            foreach (PlayerProgress other in players)
            {
                if (other.RestartReady)
                {
                    state.ReadyPlayers++;
                }
            }

            return true;
        }

        public bool TryToggleRestartReadiness(uint revision)
        {
            if (!TryRead(out RunResultState state) || !state.IsFinished || state.Revision != revision
                || !TryGetPlayer(out Entity player, out Entity connection) || !IsOwnedPlayer(player))
            {
                return false;
            }

            EntityManager entityManager = _world.EntityManager;
            Entity request = entityManager.CreateEntity();
            entityManager.AddComponentData(request, new ShopRequest
            {
                Action = ShopAction.Restart,
                Revision = revision
            });
            entityManager.AddComponentData(request, new SendRpcCommandRequest
            {
                TargetConnection = connection
            });
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_world.IsCreated)
            {
                _players.Dispose();
                _allPlayers.Dispose();
                _connections.Dispose();
            }
        }

        private bool TryGetPlayer(out Entity player, out Entity connection)
        {
            player = Entity.Null;
            connection = Entity.Null;
            if (_disposed || !_world.IsCreated || _players.IsEmptyIgnoreFilter
                || _connections.CalculateEntityCount() != 1)
            {
                return false;
            }

            connection = _connections.GetSingletonEntity();
            EntityManager entityManager = _world.EntityManager;
            int localNumber = entityManager.GetComponentData<NetworkId>(connection).Value;
            int targetNumber = _playerNumber > 0 ? _playerNumber : localNumber;
            using NativeArray<Entity> players = _players.ToEntityArray(Allocator.Temp);
            foreach (Entity candidate in players)
            {
                if (entityManager.GetComponentData<GhostOwner>(candidate).NetworkId != targetNumber)
                {
                    continue;
                }

                if (player != Entity.Null)
                {
                    return false;
                }

                player = candidate;
            }

            return targetNumber > 0 && player != Entity.Null && (_playerNumber > 0 || IsOwnedPlayer(player));
        }

        private bool IsOwnedPlayer(Entity player)
        {
            EntityManager manager = _world.EntityManager;
            return manager.HasComponent<GhostOwnerIsLocal>(player) && manager.IsComponentEnabled<GhostOwnerIsLocal>(player)
                && manager.GetComponentData<GhostOwner>(player).NetworkId == _connections.GetSingleton<NetworkId>().Value;
        }

        private static RunPhase MapPhase(WavePhase phase)
        {
            return phase switch
            {
                WavePhase.Spawning => RunPhase.Combat,
                WavePhase.WaveCleared => RunPhase.WaveCleared,
                WavePhase.Shop => RunPhase.Shop,
                WavePhase.Rewards => RunPhase.Rewards,
                WavePhase.Complete => RunPhase.Victory,
                WavePhase.Defeat => RunPhase.Defeat,
                _ => RunPhase.Waiting
            };
        }
    }
}
