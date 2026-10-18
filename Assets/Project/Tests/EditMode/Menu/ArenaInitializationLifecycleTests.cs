using System.Reflection;
using HordeMuncher.Client.Menu;
using NUnit.Framework;
using Unity.Entities;
using Unity.NetCode;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace HordeMuncher.Tests.Menu
{
    public sealed class ArenaInitializationLifecycleTests
    {
        private GameObject _owner;
        private ArenaSession _session;
        private Keyboard _keyboard;
        private SessionInitializationFailureDriverConstructor _driver;
        private INetworkStreamDriverConstructor _previousDriver;
        private RenderPipelineAsset _previousRenderPipeline;
        private RenderPipelineAsset _previousQualityRenderPipeline;
        private bool _previousRunInBackground;
        private ushort _previousAutoConnectPort;

        [SetUp]
        public void SetUp()
        {
            _previousDriver = NetworkStreamReceiveSystem.DriverConstructor;
            _previousRenderPipeline = GraphicsSettings.defaultRenderPipeline;
            _previousQualityRenderPipeline = QualitySettings.renderPipeline;
            _previousRunInBackground = Application.runInBackground;
            _previousAutoConnectPort = ClientServerBootstrap.AutoConnectPort;
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _driver = new SessionInitializationFailureDriverConstructor();
            NetworkStreamReceiveSystem.DriverConstructor = _driver;
            _owner = new GameObject("Initialization lifecycle test");
            _session = _owner.AddComponent<ArenaSession>();
        }

        [TearDown]
        public void TearDown()
        {
            _session.StopSession();
            Object.DestroyImmediate(_owner);
            InputSystem.RemoveDevice(_keyboard);
            NetworkStreamReceiveSystem.DriverConstructor = _previousDriver;
            GraphicsSettings.defaultRenderPipeline = _previousRenderPipeline;
            QualitySettings.renderPipeline = _previousQualityRenderPipeline;
            Application.runInBackground = _previousRunInBackground;
            ClientServerBootstrap.AutoConnectPort = _previousAutoConnectPort;
        }

        [Test]
        public void DisablingSessionDisposesWorldsAndPreventsNewConnectionsUntilEnabled()
        {
            Assert.That(_session.StartSession(ArenaMode.Client, "127.0.0.1", "7979"), Is.True, _session.Status);
            World client = _session.LocalClientWorlds[0];
            _session.enabled = false;
            InvokeCallback(_session, "OnDisable");

            Assert.That(client.IsCreated, Is.False);
            Assert.That(_session.IsRunning, Is.False);
            Assert.That(_session.LocalClientWorlds, Is.Empty);
            Assert.That(_session.StartSession(ArenaMode.Client, "127.0.0.1", "7979"), Is.False);
            Assert.That(_session.StartRelaySessionAsync(true).IsCompleted, Is.True);
            Assert.That(_session.IsConnecting, Is.False);

            _session.enabled = true;
            Assert.That(_session.StartSession(ArenaMode.Client, "127.0.0.1", "7979"), Is.True, _session.Status);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedClientInitializationReleasesOnlyTheFailedWorld(bool additionalPlayer)
        {
            World primary = null;
            if (additionalPlayer)
            {
                Assert.That(_session.StartSession(ArenaMode.Client, "127.0.0.1", "7979"), Is.True, _session.Status);
                primary = _session.LocalClientWorlds[0];
                using EntityQuery group = primary.EntityManager.CreateEntityQuery(typeof(LocalPlayerGroup));
                primary.EntityManager.SetComponentData(group.GetSingletonEntity(), new LocalPlayerGroup { Id = 1, JoinToken = "test-token" });
            }

            _driver.FailClientInitialization = true;
            bool started = additionalPlayer ? _session.AddLocalPlayer(2, 0) : _session.StartSession(ArenaMode.Client, "127.0.0.1", "7979");

            Assert.That(started, Is.False);
            Assert.That(_driver.LastClientWorld.IsCreated, Is.False);
            Assert.That(_session.LocalClientWorlds.Count, Is.EqualTo(additionalPlayer ? 1 : 0));
            Assert.That(_session.IsRunning, Is.EqualTo(additionalPlayer));
            Assert.That(NetworkStreamReceiveSystem.DriverConstructor, Is.SameAs(_driver));
            if (additionalPlayer)
            {
                Assert.That(primary.IsCreated, Is.True);
                Assert.That(_session.LocalClientWorlds[0], Is.SameAs(primary));
            }
        }

        [Test]
        public void DisablingApplicationReleasesUiBeforeWorldsAndReenableOnlyRebindsUi()
        {
            ArenaMenu menu = _owner.AddComponent<ArenaMenu>();
            UIDocument document = _owner.GetComponent<UIDocument>();
            document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Project/UI/ArenaPanelSettings.asset");
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/ArenaMenu.uxml");
            _owner.AddComponent<ShopUiCompositionRoot>();
            PlayerStatusUiCompositionRoot status = _owner.AddComponent<PlayerStatusUiCompositionRoot>();
            status.HudLayout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/CombatHud.uxml");
            status.PlayerLayout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/PlayerHud.uxml");
            ArenaApplication application = _owner.AddComponent<ArenaApplication>();
            InvokeCallback(application, "Start");
            VisualElement root = document.rootVisualElement;
            Assert.That(root.Q("shopOverlay"), Is.Not.Null);
            Assert.That(_session.StartSession(ArenaMode.Client, "127.0.0.1", "7979"), Is.True, _session.Status);
            World client = _session.LocalClientWorlds[0];
            InvokeCallback(status, "Update");

            application.enabled = false;
            InvokeCallback(application, "OnDisable");

            Assert.That(client.IsCreated, Is.False);
            Assert.That(root.Q("shopOverlay"), Is.Null);
            Assert.That(root.Q(className: "combat-number-layer"), Is.Null);
            InvokeCallback(menu, "Update");
            InvokeCallback(status, "LateUpdate");

            typeof(ArenaApplication).GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(application, new ArenaStartupOptions(new string[0], "server", "127.0.0.1", "7979"));
            application.enabled = true;
            InvokeCallback(application, "OnEnable");
            Assert.That(root.Q("shopOverlay"), Is.Null, "Composition waits for the document's OnEnable.");
            InvokeCallback(application, "Update");
            Assert.That(root.Query(name: "shopOverlay").ToList().Count, Is.EqualTo(1));
            Assert.That(_session.IsRunning, Is.False);
        }

        [Test]
        public void DisablingMenuCancelsAnUnfinishedLobbyHold()
        {
            ArenaMenu menu = _owner.AddComponent<ArenaMenu>();
            var lobby = new LobbySessionGateway(_session, null);
            menu.enabled = false;
            menu.Init(new ArenaSessionGateway(_session), lobby, new PlayerSeparationWarningStateSource(_session), "127.0.0.1", "7979");
            LobbyJoinHoldTracker holds = (LobbyJoinHoldTracker)typeof(LobbySessionGateway)
                .GetField("_joinHolds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lobby);
            holds.UpdateHold(1, 0, true, 0);
            Assert.That(holds.ReadProgress(1, 0, 0.3f), Is.GreaterThan(0));

            InvokeCallback(menu, "OnDisable");

            Assert.That(holds.ReadProgress(1, 0, 1), Is.Zero);
            Assert.That(holds.UpdateHold(1, 0, true, 1), Is.False, "A new hold must start from the beginning.");
        }

        private static void InvokeCallback(MonoBehaviour component, string methodName)
        {
            component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null);
        }
    }
}
