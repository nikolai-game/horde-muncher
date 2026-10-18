using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace HordeMuncher.Tests.Menu
{
    public sealed class ArenaClientSessionTests
    {
        [TestCase(ArenaMode.Host, true)]
        [TestCase(ArenaMode.Local, true)]
        [TestCase(ArenaMode.Client, false)]
        public void RoomCreatorJoinsSelectionOnceAfterLobbyArrives(ArenaMode mode, bool shouldJoinSelection)
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var sessionObject = new GameObject("Room creator selection test");
            ArenaSession session = sessionObject.AddComponent<ArenaSession>();
            bool previousRunInBackground = Application.runInBackground;
            ushort previousAutoConnectPort = ClientServerBootstrap.AutoConnectPort;
            RenderPipelineAsset previousRenderPipeline = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset previousQualityRenderPipeline = QualitySettings.renderPipeline;
            try
            {
                GraphicsSettings.defaultRenderPipeline = null;
                QualitySettings.renderPipeline = null;
                using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                    System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
                socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
                string port = ((System.Net.IPEndPoint)socket.LocalEndPoint).Port.ToString();
                socket.Close();
                Assert.That(session.StartSession(mode, "127.0.0.1", port), Is.True, session.Status);
                World clientWorld = session.LocalClientWorlds[0];
                EntityManager manager = clientWorld.EntityManager;
                using EntityQuery connections = manager.CreateEntityQuery(typeof(NetworkStreamConnection));
                Entity connection = connections.GetSingletonEntity();
                manager.AddComponentData(connection, new NetworkId { Value = 1 });
                manager.AddComponent<NetworkStreamInGame>(connection);
                manager.CreateEntity(typeof(ArenaPlayer));
                using EntityQuery selectionRequests = manager.CreateEntityQuery(typeof(LobbySelectionRpc));
                MethodInfo updateSession = typeof(ArenaSession).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                updateSession.Invoke(session, null);
                Assert.That(selectionRequests.IsEmptyIgnoreFilter, Is.True, "Wait for the authoritative lobby before joining selection.");

                using EntityQuery groups = manager.CreateEntityQuery(typeof(LocalPlayerGroup));
                manager.SetComponentData(groups.GetSingletonEntity(), new LocalPlayerGroup { Id = 1 });
                using EntityQuery snapshots = manager.CreateEntityQuery(typeof(LobbyClientSnapshot));
                Entity snapshot = snapshots.GetSingletonEntity();
                manager.SetComponentData(snapshot, new LobbyClientSnapshot { Players = "1,1,0,0,Keyboard,0,0,0,Host" });
                updateSession.Invoke(session, null);
                Assert.That(selectionRequests.CalculateEntityCount(), Is.EqualTo(shouldJoinSelection ? 1 : 0));
                if (shouldJoinSelection)
                {
                    Assert.That(selectionRequests.GetSingleton<LobbySelectionRpc>().Joined, Is.True);
                    Assert.That(selectionRequests.GetSingleton<LobbySelectionRpc>().CharacterId, Is.EqualTo(CharacterArchetypes.BALANCED));
                    manager.DestroyEntity(selectionRequests);
                }

                updateSession.Invoke(session, null);
                Assert.That(selectionRequests.IsEmptyIgnoreFilter, Is.True, "Automatic joining must not repeat after leaving selection.");
            }
            finally
            {
                Object.DestroyImmediate(sessionObject);
                InputSystem.RemoveDevice(keyboard);
                Application.runInBackground = previousRunInBackground;
                ClientServerBootstrap.AutoConnectPort = previousAutoConnectPort;
                GraphicsSettings.defaultRenderPipeline = previousRenderPipeline;
                QualitySettings.renderPipeline = previousQualityRenderPipeline;
            }
        }

        [Test]
        public void ClientCreatesFirstWorldBeforeSessionIsRunningAndCanRestartAfterStop()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var sessionObject = new GameObject("Client session test");
            ArenaSession session = sessionObject.AddComponent<ArenaSession>();
            bool previousRunInBackground = Application.runInBackground;
            ushort previousAutoConnectPort = ClientServerBootstrap.AutoConnectPort;
            RenderPipelineAsset previousRenderPipeline = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset previousQualityRenderPipeline = QualitySettings.renderPipeline;
            try
            {
                GraphicsSettings.defaultRenderPipeline = null;
                QualitySettings.renderPipeline = null;
                Assert.That(session.IsRunning, Is.False);
                Assert.That(session.AddLocalPlayer(1, 0), Is.False);
                Assert.That(session.StartSession(ArenaMode.Client, "127.0.0.1", "7979"), Is.True, session.Status);
                Assert.That(session.IsRunning, Is.True);
                Assert.That(session.Mode, Is.EqualTo(ArenaMode.Client));
                Assert.That(session.LocalClientWorlds.Count, Is.EqualTo(1));
                World clientWorld = session.LocalClientWorlds[0];
                using (EntityQuery connections = clientWorld.EntityManager.CreateEntityQuery(typeof(NetworkStreamConnection)))
                {
                    Assert.That(connections.CalculateEntityCount(), Is.EqualTo(1));
                }

                Assert.That(session.StartSession(ArenaMode.Client, "127.0.0.1", "7979"), Is.False);
                session.StopSession();
                Assert.That(session.IsRunning, Is.False);
                Assert.That(session.LocalClientWorlds, Is.Empty);
                Assert.That(session.AddLocalPlayer(1, 0), Is.False);
                Assert.That(session.StartSession(ArenaMode.Client, "127.0.0.1", "7979"), Is.True, session.Status);
            }
            finally
            {
                Object.DestroyImmediate(sessionObject);
                InputSystem.RemoveDevice(keyboard);
                Application.runInBackground = previousRunInBackground;
                ClientServerBootstrap.AutoConnectPort = previousAutoConnectPort;
                GraphicsSettings.defaultRenderPipeline = previousRenderPipeline;
                QualitySettings.renderPipeline = previousQualityRenderPipeline;
            }
        }
    }
}
