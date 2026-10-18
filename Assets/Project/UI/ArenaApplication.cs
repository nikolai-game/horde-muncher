using System;
using HordeMuncher.Client.Menu;
using UnityEngine;

namespace HordeMuncher
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ArenaSession))]
    public sealed class ArenaApplication : MonoBehaviour
    {
        [SerializeField] private string _serverAddress = ArenaConnectionDefaults.LOOPBACK_ADDRESS;
        [SerializeField] private string _port = ArenaConnectionDefaults.PORT;

        private ArenaSession _session;
        private ArenaSessionGateway _sessionGateway;
        private ArenaStartupOptions _options;
        private bool _needsComposition;

        private void Start()
        {
#if UNITY_SERVER
            const string DEFAULT_MODE = "server";
#else
            const string DEFAULT_MODE = "menu";
#endif
            _options = new ArenaStartupOptions(Environment.GetCommandLineArgs(), DEFAULT_MODE, _serverAddress, _port);
            _session = GetComponent<ArenaSession>();
            _sessionGateway = new ArenaSessionGateway(_session);
            ComposeScene();
            _options.StartSession(_sessionGateway);
        }

        private void OnEnable()
        {
            _needsComposition = _options != null;
        }

        private void Update()
        {
            if (_needsComposition)
            {
                ComposeScene();
            }
        }

        private void OnDisable()
        {
            _needsComposition = false;
            GetComponent<ArenaMenu>()?.Shutdown();
            GetComponent<ShopUiCompositionRoot>()?.Shutdown();
            GetComponent<PlayerStatusUiCompositionRoot>()?.Shutdown();
            GetComponent<PlayerGroundGlow>()?.Init(null);
            if (_session != null)
            {
                _session.StopSession();
            }
        }

        private void ComposeScene()
        {
            _needsComposition = false;
            PlayerGroundGlow groundGlow = GetComponent<PlayerGroundGlow>();
            groundGlow?.Init(_session);
            var lobbyGateway = new LobbySessionGateway(_session, groundGlow != null ? groundGlow.Settings : null);
            var warningState = new PlayerSeparationWarningStateSource(_session);
            GetComponent<ArenaMenu>()?.Init(_sessionGateway, lobbyGateway, warningState, _options.ServerAddress, _options.Port);
            ShopUiCompositionRoot shop = GetComponent<ShopUiCompositionRoot>();
            if (shop != null)
            {
                EconomySettings settings = shop.Settings != null ? shop.Settings : GetComponent<PlayerWeaponVisuals>()?.Settings;
                shop.Init(_session, settings);
            }

            GetComponent<PlayerStatusUiCompositionRoot>()?.Init(_session);
        }
    }
}
