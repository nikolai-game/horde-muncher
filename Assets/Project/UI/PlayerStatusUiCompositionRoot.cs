using System.Collections.Generic;
using HordeMuncher.Client.PlayerStatus;
using HordeMuncher.UI.Contracts;
using HordeMuncher.UI.Hud;
using HordeMuncher.UI.RunResult;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UIElements;

namespace HordeMuncher
{
    [RequireComponent(typeof(UIDocument), typeof(ArenaSession))]
    [DefaultExecutionOrder(100)]
    [MovedFrom(true, "Brotato3D", "Brotato3D.Runtime", "CombatPresentation")]
    public sealed class PlayerStatusUiCompositionRoot : MonoBehaviour
    {
        public VisualTreeAsset HudLayout;
        public VisualTreeAsset PlayerLayout;
        private List<ClientArenaPlayer> _players = new List<ClientArenaPlayer>();
        private readonly List<World> _worlds = new List<World>();
        private readonly List<ClientPlayerStatusGateway> _gateways = new List<ClientPlayerStatusGateway>();
        private ArenaSession _session;
        private VisualElement _documentRoot;
        private HudPresenter _hudPresenter;
        private RunResultPresenter _resultPresenter;
        private RunResultView _resultView;
        private CombatNumberPresenter _combatNumberPresenter;
        private ClientCombatNumberGateway _combatNumberGateway;
        private float _nextRefresh;

        private void OnDisable()
        {
            UnbindDocument();
        }

        private void LateUpdate()
        {
            _hudPresenter?.RefreshWaveCompletionAnimation(Time.unscaledDeltaTime);
            _combatNumberPresenter?.Refresh(Time.unscaledTime);
        }

        private void Update()
        {
            if (_session == null)
            {
                return;
            }

            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            if (root != _documentRoot || Time.unscaledTime >= _nextRefresh)
            {
                List<ClientArenaPlayer> players = _session.HasStarted
                    ? ClientArenaRoster.Read(_session.LocalClientWorlds) : new List<ClientArenaPlayer>();
                if (root != _documentRoot || !WorldsMatch() || !PlayersMatch(players))
                {
                    UnbindDocument();
                    _players = players;
                    BindDocument(root);
                }
            }

            if (_documentRoot == null)
            {
                return;
            }

            if (Time.unscaledTime >= _nextRefresh)
            {
                _hudPresenter.Refresh();
                _resultPresenter.Refresh(Time.unscaledTime);
                _nextRefresh = Time.unscaledTime + 0.1f;
            }

            if (!Application.isFocused)
            {
                return;
            }

            for (int clientIndex = 0; clientIndex < _worlds.Count; clientIndex++)
            {
                bool submit = ArenaLocalInput.TryGetBinding(_worlds[clientIndex], out LocalPlayerInput input)
                    && ArenaLocalInput.WasSubmitPressed(input);

                if (submit)
                {
                    _resultPresenter.ToggleRestartReadiness(clientIndex, Time.unscaledTime);
                }
            }
        }

        public void Init(ArenaSession session)
        {
            UnbindDocument();
            _session = session;
        }

        public void Shutdown()
        {
            UnbindDocument();
            _session = null;
        }

        private void BindDocument(VisualElement root)
        {
            if (root == null || HudLayout == null || PlayerLayout == null)
            {
                return;
            }

            foreach (World world in _session.LocalClientWorlds)
            {
                if (!world.IsCreated)
                {
                    return;
                }
            }

            _documentRoot = root;
            var hudSources = new List<IHudStateSource>();
            var resultSources = new List<IRunResultStateSource>();
            var restartCommands = new List<IRunRestartCommands>();
            foreach (World world in _session.LocalClientWorlds)
            {
                var gateway = new ClientPlayerStatusGateway(world);
                _worlds.Add(world);
                _gateways.Add(gateway);
                resultSources.Add(gateway);
                restartCommands.Add(gateway);
            }

            foreach (ClientArenaPlayer player in _players)
            {
                var gateway = new ClientPlayerStatusGateway(player.World, player.PlayerNumber);
                _gateways.Add(gateway);
                hudSources.Add(gateway);
            }

            TemplateContainer hud = HudLayout.Instantiate();
            hud.pickingMode = PickingMode.Ignore;
            hud.style.position = Position.Absolute;
            hud.style.left = 0;
            hud.style.right = 0;
            hud.style.top = 0;
            hud.style.bottom = 0;
            root.Add(hud);
            _hudPresenter = new HudPresenter(hudSources, hud, () => PlayerLayout.Instantiate());
            _resultView = new RunResultView(root.Q("runResult"));
            _resultPresenter = new RunResultPresenter(resultSources, restartCommands, _resultView);
            if (_worlds.Count > 0)
            {
                _combatNumberGateway = new ClientCombatNumberGateway(_worlds[0], root);
                _combatNumberPresenter = new CombatNumberPresenter(_combatNumberGateway, root);
            }

            _nextRefresh = 0;
        }

        private void UnbindDocument()
        {
            _combatNumberPresenter?.Dispose();
            _combatNumberPresenter = null;
            _combatNumberGateway?.Dispose();
            _combatNumberGateway = null;
            _hudPresenter?.Dispose();
            _resultPresenter?.Dispose();
            _resultView?.Dispose();
            foreach (ClientPlayerStatusGateway gateway in _gateways)
            {
                gateway.Dispose();
            }

            _gateways.Clear();
            _worlds.Clear();
            _hudPresenter = null;
            _resultPresenter = null;
            _resultView = null;
            _documentRoot = null;
        }

        private bool PlayersMatch(List<ClientArenaPlayer> players)
        {
            if (_players.Count != players.Count)
            {
                return false;
            }

            for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
            {
                ClientArenaPlayer previous = _players[playerIndex];
                ClientArenaPlayer current = players[playerIndex];
                if (previous.PlayerNumber != current.PlayerNumber || previous.World != current.World || previous.LocalIndex != current.LocalIndex)
                {
                    return false;
                }
            }

            return true;
        }

        private bool WorldsMatch()
        {
            if (_worlds.Count != _session.LocalClientWorlds.Count)
            {
                return false;
            }

            for (int clientIndex = 0; clientIndex < _worlds.Count; clientIndex++)
            {
                if (!_worlds[clientIndex].IsCreated || _worlds[clientIndex] != _session.LocalClientWorlds[clientIndex])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
