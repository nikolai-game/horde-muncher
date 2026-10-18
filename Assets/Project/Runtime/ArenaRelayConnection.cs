using System;
using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace HordeMuncher
{
    public sealed class ArenaRelayConnection
    {
        private const int RELAY_PEER_CAPACITY = ArenaLimits.MAX_PLAYERS - 1;
        private const int PROFILE_SUFFIX_LENGTH = 16;

        private static Task _authenticationTask;

        public bool IsHost { get; private set; }
        public string RoomCode { get; private set; }
        public RelayServerData ServerData { get; private set; }
        public RelayServerData ClientData { get; private set; }

        public static async Task<ArenaRelayConnection> CreateRoomAsync()
        {
            await EnsureAuthenticationAsync();
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(RELAY_PEER_CAPACITY);
            string roomCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            return new ArenaRelayConnection
            {
                IsHost = true,
                RoomCode = roomCode,
                ServerData = AllocationUtils.ToRelayServerData(allocation, "dtls")
            };
        }

        public static async Task<ArenaRelayConnection> JoinRoomAsync(string roomCode)
        {
            await EnsureAuthenticationAsync();
            string normalizedCode = (roomCode ?? string.Empty).Trim().ToUpperInvariant();
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(normalizedCode);
            return new ArenaRelayConnection
            {
                RoomCode = normalizedCode,
                ClientData = AllocationUtils.ToRelayServerData(allocation, "dtls")
            };
        }

        private static Task EnsureAuthenticationAsync()
        {
            if (_authenticationTask == null || _authenticationTask.IsFaulted || _authenticationTask.IsCanceled)
            {
                _authenticationTask = InitializeAuthenticationAsync();
            }

            return _authenticationTask;
        }

        private static async Task InitializeAuthenticationAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
                options.SetProfile("arena" + Guid.NewGuid().ToString("N").Substring(0, PROFILE_SUFFIX_LENGTH));
                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }
    }
}
