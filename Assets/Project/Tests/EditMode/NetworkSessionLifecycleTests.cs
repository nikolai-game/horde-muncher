using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using HordeMuncher.Client.Menu;
using NUnit.Framework;
using Unity.Collections;
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
    public sealed class NetworkSessionLifecycleTests
    {
        private readonly List<ArenaSession> _sessions = new List<ArenaSession>();
        private readonly List<World> _createdWorlds = new List<World>();
        private Keyboard _keyboard;
        private INetworkStreamDriverConstructor _previousDriverConstructor;
        private bool _driverChanged;

        [UnityTest]
        [Explicit("Four real UDP clients; host loss ends the session without migration.")]
        public IEnumerator FourClientsStartThenHostLossDisposesEverySessionWorld()
        {
            yield return new EnterPlayMode();
            yield return PrepareScene();
            yield return StartFourClients();
            World[] remoteWorlds = _sessions.Skip(1).SelectMany(session => session.LocalClientWorlds).ToArray();
            _sessions[0].StopSession();
            yield return WaitForState(() => _sessions.All(session => !session.IsRunning), "Clients did not end after host loss");
            Assert.That(remoteWorlds.All(world => !world.IsCreated), Is.True);
            foreach (ArenaSession remote in _sessions.Skip(1))
            {
                Assert.That(remote.Status, Does.Contain("хостом потеряно"));
            }

            AssertWorldsDisposed();
        }

        [UnityTest]
        [Explicit("Cancel pending UDP connection, reconnect, then exit through the menu gateway.")]
        public IEnumerator CancelConnectionAndExitToMenuDisposeWorldsAndAllowRestart()
        {
            yield return new EnterPlayMode();
            yield return PrepareScene();
            ArenaSession session = _sessions[0];
            Assert.That(session.StartSession(ArenaMode.Client, "127.0.0.1", FindAvailablePort()), Is.True);
            TrackWorlds();
            new ArenaSessionGateway(session).StopSession();
            Assert.That(session.IsConnecting, Is.False);
            Assert.That(session.IsRunning, Is.False);
            AssertWorldsDisposed();
            yield return null;
            Assert.That(session.StartSession(ArenaMode.Local, "127.0.0.1", FindAvailablePort()), Is.True);
            TrackWorlds();
            yield return WaitForState(() => session.HasJoinedLobby, "Session could not start after cancellation");
            new ArenaSessionGateway(session).StopSession();
            Assert.That(session.LocalClientWorlds, Is.Empty);
            AssertWorldsDisposed();
        }

        [UnityTest]
        [Explicit("Verify rejection reason over UDP for both protocol and content mismatches.")]
        public IEnumerator IncompatibleClientsSeeReasonBeforeMatchAndDisposeWorlds()
        {
            yield return new EnterPlayMode();
            yield return PrepareScene();
            ArenaSession server = _sessions[0];
            string port = FindAvailablePort();
            Assert.That(server.StartSession(ArenaMode.Server, "127.0.0.1", port), Is.True);
            ArenaSession client = AddSession();
            foreach (bool changeProtocol in new[] { true, false })
            {
                Assert.That(client.StartSession(ArenaMode.Client, "127.0.0.1", port), Is.True);
                TrackWorlds();
                World world = client.LocalClientWorlds[0];
                using (EntityQuery compatibility = world.EntityManager.CreateEntityQuery(typeof(ArenaCompatibility)))
                {
                    ArenaCompatibility version = ArenaCompatibility.Current;
                    if (changeProtocol)
                    {
                        version.ProtocolVersion++;
                    }
                    else
                    {
                        version.ContentVersion++;
                    }

                    world.EntityManager.SetComponentData(compatibility.GetSingletonEntity(), version);
                }

                yield return WaitForState(() => !client.IsRunning, "Incompatible client remained in session");
                Assert.That(client.Status, Does.Contain(changeProtocol ? "протокола" : "контента"));
                Assert.That(world.IsCreated, Is.False);
                using EntityQuery members = FindServerWorld().EntityManager.CreateEntityQuery(typeof(LobbyMember));
                Assert.That(members.IsEmptyIgnoreFilter, Is.True);
            }

            server.StopSession();
            AssertWorldsDisposed();
        }

        [UnityTest]
        [Explicit("Reproducible server SimulationSystemGroup timing: four UDP clients, 256 enemies, 60 Hz.")]
        public IEnumerator MeasureServerTicksWithFourClientsAnd256Enemies()
        {
            yield return new EnterPlayMode();
            yield return PrepareScene();
            yield return StartFourClients();
            World server = FindServerWorld();
            EntityManager manager = server.EntityManager;
            using (EntityQuery tickRate = manager.CreateEntityQuery(typeof(ClientServerTickRate)))
            {
                tickRate.TryGetSingleton(out ClientServerTickRate rate);
                rate.ResolveDefaults();
                rate.SimulationTickRate = 60;
                rate.NetworkTickRate = 60;
                rate.MaxSimulationStepsPerFrame = 1;
                rate.MaxSimulationStepBatchSize = 1;
                if (tickRate.IsEmptyIgnoreFilter)
                {
                    manager.AddComponentData(manager.CreateEntity(), rate);
                }
                else
                {
                    manager.SetComponentData(tickRate.GetSingletonEntity(), rate);
                }
            }

            using (EntityQuery configuration = manager.CreateEntityQuery(typeof(ArenaConfiguration)))
            using (EntityQuery enemies = manager.CreateEntityQuery(typeof(ArenaEnemy)))
            using (EntityQuery pendingSpawns = manager.CreateEntityQuery(typeof(PendingEnemySpawn)))
            {
                manager.DestroyEntity(enemies);
                manager.DestroyEntity(pendingSpawns);
                Entity prefab = configuration.GetSingleton<ArenaConfiguration>().EnemyPrefab;
                var random = new Unity.Mathematics.Random(7391);
                for (int index = 0; index < 256; index++)
                {
                    Entity enemy = manager.Instantiate(prefab);
                    float2 position = random.NextFloat2(-18, 18);
                    manager.SetComponentData(enemy, LocalTransform.FromPosition(position.x, 0.65f, position.y));
                    manager.SetComponentData(enemy, new CombatHealth { Current = 1000000, Maximum = 1000000 });
                    ArenaEnemy stats = manager.GetComponentData<ArenaEnemy>(enemy);
                    stats.ContactDamage = 0;
                    manager.SetComponentData(enemy, stats);
                }
            }

            using (EntityQuery settings = manager.CreateEntityQuery(typeof(WaveRuntimeSettings)))
            {
                WaveRuntimeSettings value = settings.GetSingleton<WaveRuntimeSettings>();
                value.MaxAlive = 64;
                manager.SetComponentData(settings.GetSingletonEntity(), value);
            }

            using (EntityQuery players = manager.CreateEntityQuery(typeof(ArenaPlayer), typeof(CombatHealth)))
            using (NativeArray<Entity> entities = players.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity player in entities)
                {
                    manager.SetComponentData(player, new CombatHealth { Current = 1000000, Maximum = 1000000 });
                }
            }

            SimulationSystemGroup simulation = server.GetExistingSystemManaged<SimulationSystemGroup>();
            ServerTickMeasurementStartSystem start = server.CreateSystemManaged<ServerTickMeasurementStartSystem>();
            ServerTickMeasurementEndSystem end = server.CreateSystemManaged<ServerTickMeasurementEndSystem>();
            end.Start = start;
            simulation.AddSystemToUpdateList(start);
            simulation.AddSystemToUpdateList(end);
            simulation.SortSystems();
            yield return WaitForState(() => end.TickDurationsMilliseconds.Count >= 120, "Server tick measurement did not finish");
            double[] tickDurationsMilliseconds = end.TickDurationsMilliseconds.Take(120).OrderBy(value => value).ToArray();
            TestContext.WriteLine($"Server SimulationSystemGroup, Editor {Application.unityVersion}, {SystemInfo.processorType}; "
                + $"4 UDP clients, 256 enemies, seed=7391, warmup=30, tickDurationsMilliseconds=120; "
                + $"meanMs={tickDurationsMilliseconds.Average():F3}; medianMs={tickDurationsMilliseconds[60]:F3}; "
                + $"p95Ms={tickDurationsMilliseconds[113]:F3}; maxMs={tickDurationsMilliseconds[119]:F3}; "
                + $"enemyRange={end.MinimumEnemies}..{end.MaximumEnemies}; projectilePeak={end.MaximumProjectiles}; "
                + $"deltaTimeRange={end.MinimumDeltaTime:F5}..{end.MaximumDeltaTime:F5}");
            Assert.That(end.MinimumEnemies, Is.EqualTo(256));
            Assert.That(end.MaximumEnemies, Is.EqualTo(256));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (ArenaSession session in _sessions)
            {
                if (session != null)
                {
                    session.StopSession();
                }
            }

            AssertWorldsDisposed();
            _sessions.Clear();
            _createdWorlds.Clear();
            if (_driverChanged)
            {
                NetworkStreamReceiveSystem.DriverConstructor = _previousDriverConstructor;
                _driverChanged = false;
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

        private IEnumerator PrepareScene()
        {
            _previousDriverConstructor = NetworkStreamReceiveSystem.DriverConstructor;
            NetworkStreamReceiveSystem.DriverConstructor = new SessionTestUdpDriverConstructor();
            _driverChanged = true;
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync("Arena", LoadSceneMode.Single);
            while (!sceneLoad.isDone)
            {
                yield return null;
            }
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _keyboard.MakeCurrent();
            _sessions.Add(UnityEngine.Object.FindFirstObjectByType<ArenaSession>());
            Assert.That(_sessions[0], Is.Not.Null);
        }

        private IEnumerator StartFourClients()
        {
            string port = FindAvailablePort();
            Assert.That(_sessions[0].StartSession(ArenaMode.Host, "127.0.0.1", port), Is.True, _sessions[0].Status);
            TrackWorlds();
            yield return WaitForState(() => _sessions[0].HasJoinedLobby, "Host client did not join");
            for (int index = 1; index < 4; index++)
            {
                ArenaSession client = AddSession();
                Assert.That(client.StartSession(ArenaMode.Client, "127.0.0.1", port), Is.True, client.Status);
                TrackWorlds();
                yield return WaitForState(() => client.HasJoinedLobby, "Remote client did not join");
            }

            yield return WaitForState(() => _sessions.All(session => session.ReadLobbyPlayers().Count == 4
                && CountPlayers(session.LocalClientWorlds[0]) == 4), "Four clients did not converge on the roster");
            Assert.That(_sessions.Select(session => session.LocalPlayerGroupId).Distinct().Count(), Is.EqualTo(4));
            foreach (ArenaSession session in _sessions)
            {
                session.SetLobbySelection(0, true);
            }

            yield return WaitForState(() => _sessions.All(session => session.ReadLobbyPlayers().All(player => player.SelectionJoined)),
                "Selection did not reach every client");
            foreach (ArenaSession session in _sessions)
            {
                session.SetLobbyReady(0, true);
            }

            yield return WaitForState(() => _sessions.All(session => session.HasStarted), "Four clients did not start the match");
        }

        private ArenaSession AddSession()
        {
            ArenaSession session = new GameObject("Remote session test").AddComponent<ArenaSession>();
            _sessions.Add(session);
            return session;
        }

        private void TrackWorlds()
        {
            foreach (ArenaSession session in _sessions)
            {
                foreach (World world in session.LocalClientWorlds)
                {
                    if (!_createdWorlds.Contains(world))
                    {
                        _createdWorlds.Add(world);
                    }
                }
            }

            foreach (World world in World.All)
            {
                if (world.IsServer() && world.Name == "ArenaServer" && !_createdWorlds.Contains(world))
                {
                    _createdWorlds.Add(world);
                }
            }
        }

        private void AssertWorldsDisposed()
        {
            Assert.That(_createdWorlds.All(world => !world.IsCreated), Is.True, "A session world was not disposed");
        }

        private World FindServerWorld() => _createdWorlds.Single(world => world.IsCreated && world.IsServer());

        private static int CountPlayers(World world)
        {
            using EntityQuery players = world.EntityManager.CreateEntityQuery(typeof(ArenaPlayer));
            return players.CalculateEntityCount();
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

        private static string FindAvailablePort()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint).Port.ToString();
        }
    }
}
