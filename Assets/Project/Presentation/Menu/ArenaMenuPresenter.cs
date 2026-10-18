using System;
using HordeMuncher.UI.Contracts;

namespace HordeMuncher.UI.Menu
{
    public sealed class ArenaMenuPresenter : IDisposable
    {
        private readonly IArenaSessionStateSource _session;
        private readonly IArenaSessionCommands _commands;
        private readonly IArenaMenuView _view;
        private readonly string _serverAddress;
        private readonly string _port;
        private bool _isSettingsVisible;
        private bool _wasRunning;
        private bool _disposed;

        public ArenaMenuPresenter(IArenaSessionStateSource session, IArenaSessionCommands commands, IArenaMenuView view,
            string serverAddress, string port)
        {
            _session = session;
            _commands = commands;
            _view = view;
            _serverAddress = serverAddress;
            _port = port;
            _view.OnHostSessionClicked += HostSessionClickedHandler;
            _view.OnJoinRoomClicked += JoinRoomClickedHandler;
            _view.OnOpenSettingsClicked += OpenSettingsClickedHandler;
            _view.OnCloseSettingsClicked += CloseSettingsClickedHandler;
            _view.OnExitToMenuClicked += ExitToMenuClickedHandler;
            _view.OnResumeGameClicked += ResumeGameClickedHandler;
            Refresh();
            if (!_wasRunning)
            {
                _view.FocusHostButton();
            }
        }

        public void Refresh()
        {
            if (_disposed)
            {
                return;
            }

            ArenaSessionState state = _session.Read();
            if (state.IsRunning)
            {
                _isSettingsVisible = false;
            }

            _view.Render(state, _isSettingsVisible);
            if (_wasRunning && !state.IsRunning)
            {
                _view.FocusHostButton();
            }

            _wasRunning = state.IsRunning;
        }

        public bool CloseSettings()
        {
            if (!_isSettingsVisible || _disposed)
            {
                return false;
            }

            _isSettingsVisible = false;
            Refresh();
            _view.FocusHostButton();
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _view.OnHostSessionClicked -= HostSessionClickedHandler;
            _view.OnJoinRoomClicked -= JoinRoomClickedHandler;
            _view.OnOpenSettingsClicked -= OpenSettingsClickedHandler;
            _view.OnCloseSettingsClicked -= CloseSettingsClickedHandler;
            _view.OnExitToMenuClicked -= ExitToMenuClickedHandler;
            _view.OnResumeGameClicked -= ResumeGameClickedHandler;
        }

        private void HostSessionClickedHandler()
        {
            _commands.StartHostSession(_serverAddress, _port);
            Refresh();
        }

        private void JoinRoomClickedHandler(string roomCode)
        {
            _commands.JoinRoom(roomCode);
            Refresh();
        }

        private void OpenSettingsClickedHandler()
        {
            _isSettingsVisible = true;
            Refresh();
        }

        private void CloseSettingsClickedHandler()
        {
            CloseSettings();
        }

        private void ResumeGameClickedHandler()
        {
            _commands.ResumeGame();
            Refresh();
        }

        private void ExitToMenuClickedHandler()
        {
            _commands.StopSession();
            Refresh();
        }
    }
}
