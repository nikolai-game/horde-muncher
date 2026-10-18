using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using HordeMuncher.Editor;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HordeMuncher.Tests
{
    public sealed class NetworkSessionPlayModeTests
    {
        private ArenaSession _session;
        private Keyboard _keyboard;
        private INetworkStreamDriverConstructor _previousDriverConstructor;
        private bool _hasImpairedDriver;

        [UnityTest]
        [Explicit("Starts real Play Mode network worlds. Run this test explicitly after authorizing gameplay checks.")]
        public IEnumerator TwoClientsMoveDisconnectAndStartAnotherSession()
        {
            return VerifySession(false);
        }

        [UnityTest]
        [Explicit("Starts UDP Play Mode worlds with 100±25ms delay, 5% loss and 3% duplicated packets.")]
        public IEnumerator ImpairedUdpClientsConvergeDisconnectAndRestart()
        {
            return VerifySession(true);
        }

        private IEnumerator VerifySession(bool impairNetwork)
        {
            yield return new EnterPlayMode();
            if (impairNetwork)
            {
                _previousDriverConstructor = NetworkStreamReceiveSystem.DriverConstructor;
                NetworkStreamReceiveSystem.DriverConstructor = new ImpairedNetworkDriverConstructor();
                _hasImpairedDriver = true;
            }
            yield return SceneManager.LoadSceneAsync("Arena", LoadSceneMode.Single);
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _keyboard.MakeCurrent();
            _session = UnityEngine.Object.FindFirstObjectByType<ArenaSession>();
            Assert.That(_session, Is.Not.Null);
            string port = FindAvailablePort();
            Assert.That(_session.StartSession(ArenaMode.Local, "127.0.0.1", port), Is.True, _session.Status);
            yield return WaitForState(() => _session.HasJoinedLobby && _session.ReadLobbyPlayers().Count == 1,
                "First client did not receive its server-assigned group");
            Assert.That(_session.AddLocalPlayer(2, 0), Is.True, _session.Status);
            yield return WaitForState(() => _session.ReadLobbyPlayers().Count == 2
                && _session.LocalClientWorlds.All(world => CountPlayers(world) == 2), "Two clients did not receive the same party");

            LocalPlayerInput[] inputs = _session.ReadLocalInputs().ToArray();
            foreach (LocalPlayerInput input in inputs)
            {
                _session.SetLobbySelection(input.LocalIndex, true);
            }

            yield return WaitForState(() => _session.ReadLobbyPlayers().All(player => player.SelectionJoined),
                "Selection RPCs did not reach the server");
            foreach (LocalPlayerInput input in inputs)
            {
                _session.SetLobbyReady(input.LocalIndex, true);
            }

            yield return WaitForState(() => _session.HasStarted && _session.LocalClientWorlds.All(IsSpawning),
                "Ready RPCs did not start the wave on both clients");
            World firstClient = _session.LocalClientWorlds[0];
            if (impairNetwork)
            {
                foreach (World client in _session.LocalClientWorlds)
                {
                    using EntityQuery drivers = client.EntityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
                    Assert.That(drivers.GetSingleton<NetworkStreamDriver>().DriverStore.IsAnyUsingSimulator, Is.True);
                }
            }
            float3 previousPosition = ReadOwnedPlayerPosition(firstClient);
            int previousFrame = Time.frameCount;
            ArenaVerification.SetInput(firstClient.Name, 1, 0);
            yield return new WaitForSecondsRealtime(1);
            ArenaVerification.SetInput(firstClient.Name, 0, 0);
            Assert.That(Time.frameCount, Is.GreaterThan(previousFrame + 5), "Player loop was not advancing");
            Assert.That(ReadOwnedPlayerPosition(firstClient).x, Is.GreaterThan(previousPosition.x + 0.5f),
                "Predicted player did not move");

            World secondClient = _session.LocalClientWorlds[1];
            using (EntityQuery primaryGroup = firstClient.EntityManager.CreateEntityQuery(typeof(LocalPlayerGroup)))
            using (EntityQuery secondaryGroup = secondClient.EntityManager.CreateEntityQuery(typeof(LocalPlayerGroup)))
            {
                LocalPlayerGroup primaryMembership = primaryGroup.GetSingleton<LocalPlayerGroup>();
                LocalPlayerGroup secondaryMembership = secondaryGroup.GetSingleton<LocalPlayerGroup>();
                Assert.That(primaryMembership.Id, Is.EqualTo(_session.LocalPlayerGroupId).And.Positive);
                Assert.That(secondaryMembership.Id, Is.EqualTo(primaryMembership.Id));
                Assert.That(primaryMembership.JoinToken.IsEmpty, Is.False);
                Assert.That(secondaryMembership.JoinToken.IsEmpty, Is.True);
            }

            int ownerId;
            using (EntityQuery ownedPlayer = firstClient.EntityManager.CreateEntityQuery(typeof(ArenaPlayer),
                typeof(GhostOwnerIsLocal), typeof(GhostOwner)))
            {
                ownerId = ownedPlayer.GetSingleton<GhostOwner>().NetworkId;
            }

            yield return WaitForState(() => HasMatchingPlayerPosition(secondClient, ownerId, ReadOwnedPlayerPosition(firstClient)),
                "Observer did not receive authoritative movement snapshots");
            using (EntityQuery connections = secondClient.EntityManager.CreateEntityQuery(typeof(NetworkId)))
            {
                secondClient.EntityManager.AddComponentData(connections.GetSingletonEntity(), new NetworkStreamRequestDisconnect
                {
                    Reason = NetworkStreamDisconnectReason.ConnectionClose
                });
            }

            yield return WaitForState(() => _session.LocalClientWorlds.Count == 1 && CountPlayers(firstClient) == 1,
                "Disconnected player remained in the session");
            Assert.That(secondClient.IsCreated, Is.False, "Disconnected client world was not disposed");
            _session.StopSession();
            Assert.That(firstClient.IsCreated, Is.False, "First client world survived StopSession");
            Assert.That(_session.IsRunning, Is.False);
            Assert.That(_session.StartSession(ArenaMode.Local, "127.0.0.1", port), Is.True, _session.Status);
            yield return WaitForState(() => _session.HasJoinedLobby && _session.ReadLobbyPlayers().Count == 1
                && CountPlayers(_session.LocalClientWorlds[0]) == 1, "New session did not start with a fresh party");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_session != null)
            {
                _session.StopSession();
            }

            if (_hasImpairedDriver)
            {
                NetworkStreamReceiveSystem.DriverConstructor = _previousDriverConstructor;
                _hasImpairedDriver = false;
            }

            if (_keyboard != null && _keyboard.added)
            {
                InputSystem.RemoveDevice(_keyboard);
            }

            if (Application.isPlaying)
            {
                yield return new ExitPlayMode();
            }
        }

        private static IEnumerator WaitForState(Func<bool> condition, string failureMessage)
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), failureMessage);
                yield return null;
            }
        }

        private static int CountPlayers(World world)
        {
            using EntityQuery players = world.EntityManager.CreateEntityQuery(typeof(ArenaPlayer));
            return players.CalculateEntityCount();
        }

        private static bool IsSpawning(World world)
        {
            using EntityQuery players = world.EntityManager.CreateEntityQuery(typeof(ArenaPlayer), typeof(WaveStatus));
            using Unity.Collections.NativeArray<WaveStatus> statuses = players.ToComponentDataArray<WaveStatus>(Unity.Collections.Allocator.Temp);
            return statuses.Length == 2 && statuses.All(status => status.Phase == WavePhase.Spawning);
        }

        private static float3 ReadOwnedPlayerPosition(World world)
        {
            using EntityQuery player = world.EntityManager.CreateEntityQuery(typeof(ArenaPlayer), typeof(GhostOwnerIsLocal), typeof(LocalTransform));
            return player.GetSingleton<LocalTransform>().Position;
        }

        private static bool HasMatchingPlayerPosition(World world, int ownerId, float3 expectedPosition)
        {
            using EntityQuery players = world.EntityManager.CreateEntityQuery(typeof(ArenaPlayer), typeof(GhostOwner), typeof(LocalTransform));
            using Unity.Collections.NativeArray<Entity> entities = players.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (Entity entity in entities)
            {
                if (world.EntityManager.GetComponentData<GhostOwner>(entity).NetworkId == ownerId)
                {
                    float3 position = world.EntityManager.GetComponentData<LocalTransform>(entity).Position;
                    return math.all(math.isfinite(position)) && math.distance(position, expectedPosition) < 0.5f;
                }
            }

            return false;
        }

        private static string FindAvailablePort()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint).Port.ToString();
        }
    }
}
