using HordeMuncher.Client.Menu;
using HordeMuncher.UI.Hud;
using HordeMuncher.UI.Menu;
using UnityEngine;
using UnityEngine.UIElements;

namespace HordeMuncher
{
    [RequireComponent(typeof(UIDocument), typeof(ArenaSession))]
    public sealed class ArenaMenu : MonoBehaviour
    {
        private string _serverAddress;
        private string _port;

        private ArenaSessionGateway _sessionGateway;
        private LobbySessionGateway _lobbyGateway;
        private LobbyView _lobbyView;
        private LobbyPresenter _lobbyPresenter;
        private PlayerSeparationWarningStateSource _warningState;
        private ArenaMenuView _view;
        private ArenaMenuPresenter _menuPresenter;
        private PlayerSeparationWarningPresenter _warningPresenter;
        private VisualElement _documentRoot;

        private void OnEnable()
        {
            if (_sessionGateway != null)
            {
                BindDocument();
            }
        }

        private void Update()
        {
            if (_sessionGateway == null)
            {
                return;
            }

            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            if (root != _documentRoot)
            {
                UnbindDocument();
                BindDocument();
            }

            _lobbyGateway?.UpdateInput(_lobbyPresenter, Time.unscaledTime);
            _lobbyPresenter?.Refresh();
            _menuPresenter?.Refresh();
            _warningPresenter?.Refresh();
        }

        private void OnDisable()
        {
            UnbindDocument();
        }

        public void Init(ArenaSessionGateway sessionGateway, LobbySessionGateway lobbyGateway,
            PlayerSeparationWarningStateSource warningState, string serverAddress, string port)
        {
            UnbindDocument();
            _sessionGateway = sessionGateway;
            _lobbyGateway = lobbyGateway;
            _warningState = warningState;
            _serverAddress = serverAddress;
            _port = port;
            if (isActiveAndEnabled)
            {
                BindDocument();
            }
        }

        public void Shutdown()
        {
            UnbindDocument();
            _sessionGateway = null;
            _lobbyGateway = null;
            _warningState = null;
        }

        private void BindDocument()
        {
            _documentRoot = GetComponent<UIDocument>().rootVisualElement;
            if (_documentRoot == null)
            {
                return;
            }

            _documentRoot.RegisterCallback<NavigationCancelEvent>(NavigationCancelHandler, TrickleDown.TrickleDown);
            _documentRoot.RegisterCallback<NavigationSubmitEvent>(NavigationSubmitHandler, TrickleDown.TrickleDown);
            _documentRoot.RegisterCallback<NavigationMoveEvent>(LobbyNavigationMoveHandler, TrickleDown.TrickleDown);
            _lobbyView = new LobbyView(_documentRoot);
            _lobbyPresenter = new LobbyPresenter(_lobbyGateway, _lobbyView);
            _view = new ArenaMenuView(_documentRoot);
            _menuPresenter = new ArenaMenuPresenter(_sessionGateway, _sessionGateway, _view, _serverAddress, _port);
            _warningPresenter = new PlayerSeparationWarningPresenter(_warningState, _documentRoot.Q("sharedViewWarning"));
        }

        private void NavigationSubmitHandler(NavigationSubmitEvent navigation)
        {
            bool isResultVisible = _documentRoot.Q("runResult")?.ClassListContains("hidden") == false;
            if (_lobbyGateway.ReadLobby().IsVisible || isResultVisible)
            {
                navigation.StopImmediatePropagation();
            }
        }

        private void LobbyNavigationMoveHandler(NavigationMoveEvent navigation)
        {
            if (_lobbyGateway.ReadLobby().IsVisible)
            {
                navigation.StopImmediatePropagation();
            }
        }

        private void NavigationCancelHandler(NavigationCancelEvent navigation)
        {
            if (_menuPresenter?.CloseSettings() == true)
            {
                navigation.StopImmediatePropagation();
                return;
            }

            if (_sessionGateway.Read().IsInLobby)
            {
                navigation.StopImmediatePropagation();
                return;
            }

            if (_sessionGateway.Read().IsRunning)
            {
                if (_documentRoot.Q("runResult")?.ClassListContains("hidden") == false)
                {
                    _sessionGateway.StopSession();
                }

                navigation.StopImmediatePropagation();
            }
        }

        private void UnbindDocument()
        {
            _lobbyGateway?.ResetInput();
            _documentRoot?.UnregisterCallback<NavigationCancelEvent>(NavigationCancelHandler, TrickleDown.TrickleDown);
            _documentRoot?.UnregisterCallback<NavigationSubmitEvent>(NavigationSubmitHandler, TrickleDown.TrickleDown);
            _documentRoot?.UnregisterCallback<NavigationMoveEvent>(LobbyNavigationMoveHandler, TrickleDown.TrickleDown);
            _lobbyPresenter?.Dispose();
            _lobbyView?.Dispose();
            _lobbyPresenter = null;
            _lobbyView = null;
            _menuPresenter?.Dispose();
            _view?.Dispose();
            _warningPresenter?.Dispose();
            _menuPresenter = null;
            _view = null;
            _warningPresenter = null;
            _documentRoot = null;
        }
    }
}
